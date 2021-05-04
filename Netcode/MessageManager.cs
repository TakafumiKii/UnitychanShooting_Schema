using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Newtonsoft.Json;

namespace FakeServer.Network
{
    class MessageManager
    {
        public interface IRecieveMessage
        {
            bool RecieveMessage(MessageManager manager,MessageHeader header,byte[] data);
        }


        Task RecvTask = null;
        CancellationTokenSource CancellTokenSource = null;
        TcpClient Client;
        NetworkStream Stream;

        IRecieveMessage RecvMsg;

        List<SendMessageWork> SendList = new List<SendMessageWork>();
        public MessageManager(TcpClient client, IRecieveMessage recvMsg)
        {
            Debug.Assert(client != null);
            Debug.Assert(recvMsg != null);
            Client = client;
            Stream = Client.GetStream();

            RecvMsg = recvMsg;

            Debug.Assert(Stream != null);
        }

        ~MessageManager()
        {
            Cancel();
            Terminate();
        }

        //byte[] StructureToByteArray(object obj)
        //{
        //    int len = Marshal.SizeOf(obj);

        //    byte[] arr = new byte[len];

        //    IntPtr ptr = Marshal.AllocHGlobal(len);

        //    Marshal.StructureToPtr(obj, ptr, true);

        //    Marshal.Copy(ptr, arr, 0, len);

        //    Marshal.FreeHGlobal(ptr);

        //    return arr;
        //}

        //void ByteArrayToStructure(byte[] bytearray, ref object obj)
        //{
        //    int len = Marshal.SizeOf(obj);

        //    IntPtr i = Marshal.AllocHGlobal(len);

        //    Marshal.Copy(bytearray, 0, i, len);

        //    obj = Marshal.PtrToStructure(i, obj.GetType());

        //    Marshal.FreeHGlobal(i);
        //}

        public bool IsActive{ get { return (RecvTask != null); } }

        public Task RunRecvTask()
        {
            if (IsActive)
            {
                return null;
            }
            CancellTokenSource = new CancellationTokenSource();
            SendList.Clear();
        
            RecvTask = Task.Run(() =>
            {
                try
                {
                    byte[] tempHeader = new byte[MessageHeader.DEFAULT_HEADER_SIZE];
                    const int TEMP_DATASIZE = 1 * 1024 * 1024; //1MB
                    byte[] tempData = new byte[TEMP_DATASIZE];

                    using (NetworkStream stream = Client.GetStream())
                    {
                        while (true)
                        {
                            MessageHeader header;
                            {
                                //  ヘッダー読み込み
                                int read = stream.Read(tempHeader, 0, MessageHeader.DEFAULT_HEADER_SIZE);
                                if (read == 0)
                                {// 読み取り終了
                                    return;
                                }
                                Debug.Assert(read == MessageHeader.DEFAULT_HEADER_SIZE);

                                ArraySegment<byte> seg = new ArraySegment<byte>(tempHeader, 0, read);
                                header = new MessageHeader(seg.ToArray());
                            }

                            if (header.IsSystemMessage)
                            {
                                //  メッセージ分岐
                                Debug.Assert(header.DataSize < 10 * 1024 * 1024);   //  非常識なサイズが来ると困るので10MB制限をかけておく
                                                                                    // 読み込み先の設定
                                byte[] buffer = (header.DataSize <= tempData.Length) ? tempData : new byte[header.DataSize];    // TODO:非常識なサイズがくると困る

                                byte[] data = null;

                                if (header.DataSize > 0)
                                {// 添付データがあるので読み込んでおく
                                    int readSum = 0;
                                    while (readSum < header.DataSize)
                                    {
                                        int left = header.DataSize - readSum;
                                        int read = stream.Read(buffer, readSum, left);

                                        readSum += read;
                                    }
                                    ArraySegment<byte> seg = new ArraySegment<byte>(buffer, 0, header.DataSize);
                                    data = seg.ToArray();
                                }
                                if (!RecvMsg.RecieveMessage(this, header, data))
                                {// falseが返されたら待機を解除する
                                    break;
                                }
                            }
                            //else if(header.IsUserMessage)
                            //{// 今回は未実装

                            //}
                            else
                            {
                                throw new Exception("通信を正しく読み取ることができませんでした");
                            }
                        }
                    }
                }
                catch (IOException e)
                {// 待機中に切断された
                    Console.WriteLine(e.Message);
//                    throw;
                }
                catch (Exception e)
                {// 予期せぬ例外
                    Console.WriteLine(e.GetType().FullName + e.Message);
//                    throw;
                }
            }, CancellTokenSource.Token).ContinueWith(t =>
            {
                Terminate();
                //// TODO:あとしまつ
                //if (t.IsCanceled)
                //{
                //}
            });
            return RecvTask;
        }

        void Terminate()
        {
            Stream = null;
            Client = null;
            RecvTask = null;
            CancellTokenSource = null;
            RecvMsg = null;
        }

        public void Cancel()
        {
            if (CancellTokenSource != null)
            {
                lock (CancellTokenSource)
                {
                    if (!CancellTokenSource.IsCancellationRequested)// && RecvTask != null
                    {
                        CancellTokenSource.Cancel();
                    }
                    CancellTokenSource.Dispose();
                    CancellTokenSource = null;
                }
            }

            // TODO:時間がないので配列を一旦コピーしてデッドロックが発生しないようにしておく
            SendMessageWork[] worklist;
            lock (SendList)
            {
                worklist = new SendMessageWork[SendList.Count];
                SendList.CopyTo(worklist);
            }
            foreach (var work in worklist)
            {
                work.Cancel();
            }
        }
        public void Stop()
        {
            Cancel();
            if (RecvTask != null && Task.CurrentId != RecvTask.Id)
            {
                if(RecvTask.Status >= TaskStatus.Running && !RecvTask.IsCanceled && !RecvTask.IsFaulted && !RecvTask.IsCompleted)
                {
                    RecvTask.Wait();
                }
            }
        }

        void RecvMessage(MessageHeader header)
        {
//            ArraySegment
        }

        SendMessageWork SendMessage(byte[] header, byte[] body)
        {
            if(!IsActive)
            {
                Console.WriteLine("Failed SendMessage inactive");
                return null;
            }
            SendMessageWork sendWork = new SendMessageWork(Stream, header, body,(work)=>
                {
                    lock (SendList)
                    {
                        SendList.Remove(work);
                    }
                });
            lock (SendList)
            {
                SendList.Add(sendWork);
            }
            return sendWork;
        }

        public SendMessageWork SendSystemMessage(object obj)
        {
            string name = obj.GetType().Name;
            return SendSystemMessage(name, obj);
        }
        public SendMessageWork SendSystemMessage(string name, object obj)
        {
            MessageHeader.RawData head = new Network.MessageHeader.RawData();
            if (obj == null)
            {
                int sendSize = head.SetupSystem(name, 0);
                return SendMessage(head.ToArray(), null);
            }
            else
            {
                string text = JsonConvert.SerializeObject(obj);
                byte[] body = Encoding.UTF8.GetBytes(text);

                int sendSize = head.SetupSystem(name, body.Length);
                return SendMessage(head.ToArray(), body);
            }
        }
        public SendMessageWork SendSystemMessage(Enum enumparam,object obj)
        {
            string name = enumparam.ToString();
            return SendSystemMessage(name, obj);
        }
        // 直接データ送信(今回は未実装)
        //bool SendData(object obj)
        //{

        //}
    }
}
