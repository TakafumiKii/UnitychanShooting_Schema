using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;

using System.Diagnostics;

using FakeServer.Network.Information;
using Newtonsoft.Json;

namespace FakeServer.Network
{
    // イベント処理用のデリゲート
    //    delegate void ReciveMessage(object sender);

    class MessageClient : MessageManager.IRecieveMessage
    {
        public class RecieveMessageArgs
        {
            public MessageCommand Message { get; internal set; }
            public string JsonText { get; internal set; }
//            public object DeserializeObject { get; internal set; }
        }

        TcpClient Client;
        MessageManager MessageMan { get; set; }

        object LockObj = new object();

        public UserInfo UserInfo { get; private set; }
        public UserInfoResponse UserInfoResponse { get; private set; }
        public UserRankInfo SelfRankInfo { get; private set; }
        public UserRankInfo[] RankInfos { get; private set; }
        string UserInfoJsonText = "";
        //        public string UserInfoFilePath { get; private set; } = USER_INFO_PATH;// ユーザー情報ファイルの保存先

        public class ChangeStateArg
        {
            public StateStatus OldState { get; internal set; }
            public StateStatus NewState { get; internal set; }
        }

        public event EventHandler<ChangeStateArg> OnChangeState;
        public event EventHandler<RecieveMessageArgs> OnRecieveMessage;

        public enum StateStatus
        {
            None,
            Initialize,
            Connect,
            Active,
            Max
        }
        StateStatus _State = StateStatus.None;
        public StateStatus State
        {
            get { return _State; }
            private set
            {
                if (_State != value)
                {
                    ChangeStateArg arg = new ChangeStateArg { OldState = _State, NewState = value };
                    _State = value;
                    if(OnChangeState != null)
                    {
                        OnChangeState.Invoke(this, arg);
                    }
                }
            }
        }
        public bool IsInitialized { get { return (_State >= StateStatus.Initialize); } }
        //        bool IsInitialized { get { return (Client != null); } }
        public bool IsActive { get { return (_State == StateStatus.Active); } }

        ~MessageClient()
        {
            Terminate();
        }

        public Task Connect(string address,int portNo)
        {
            if(IsInitialized || Client != null)
            {
                return null;
            }
            Console.WriteLine("MessageClient::Start()");
            State = StateStatus.Initialize;
            return Task.Run(() => {
                // 接続処理は別タスクで実行する
                try
                {
                    lock (LockObj)
                    {
                        Client = new TcpClient();
                        Console.WriteLine("MessageClient::Connect");
                        Client.Connect(address, portNo);

                        State = StateStatus.Connect;

                        MessageMan = new MessageManager(Client, this);
                        MessageMan.RunRecvTask().ContinueWith((t) =>
                        {
                            Console.WriteLine("MessageClient::DisConnect");
                            Terminate();
                        });
                        State = StateStatus.Active;
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e.Message);
                    Terminate();
                }
                catch (Exception e)
                {// 予期せぬ例外
                    Console.WriteLine(e.GetType().FullName + e.Message);
                    Terminate();
                }
            });
            //.ContinueWith((t) => {
            //    Terminate();
            //});
        }
        void Terminate()
        {
            lock (LockObj)
            {
                if (MessageMan != null)
                {
                    MessageMan.Stop();
                    MessageMan = null;
                }
                if (Client != null)
                {
                    if (Client.Connected)
                    {
                        Client.Close();
                        Client.Dispose();
                    }
                    Client = null;
                }
                UserInfo = null;
                SelfRankInfo = null;
                RankInfos = null;

                State = StateStatus.None;
            }
        }
        public void Close()
        {
            Terminate();
        }

        public void Login(UserInfo info)
        {
            Debug.Assert(IsActive);
            MessageMan.SendSystemMessage(MessageCommand.Login, info);
        }

        public void UploadScore(int point)
        {
            Debug.Assert(IsActive);
            Debug.Assert(UserInfo.Sereal != 0);
            UserScoreParam scoreInfo = new UserScoreParam();
            scoreInfo.Sereal = UserInfo.Sereal;
            scoreInfo.Point = point;

            MessageMan.SendSystemMessage(MessageCommand.UploadUserScore, scoreInfo);
        }
        public void RequestScoreRanking(int skip,int take, UserInfo info = null)
        {
            Debug.Assert(IsActive);
            RankingRequest request = new RankingRequest();
            request.Skip = skip;
            request.Take = take;
            if(info != null)
            {
                request.Sereal = info.Sereal;
            }
            MessageMan.SendSystemMessage(MessageCommand.GetScoreRanking, request);

        }

        public bool RecieveMessage(MessageManager man, MessageHeader header, byte[] data)
        {
            string jsonText = Encoding.UTF8.GetString(data);
            Console.WriteLine("Recieve " + header.Name);
            try
            {
                MessageCommand param = (MessageCommand)Enum.Parse(typeof(MessageCommand), header.Name);
                switch (param)    
                {
                //                case "UserInfoRespon":
                case MessageCommand.ResUserInfo:
                    if (UserInfoJsonText != jsonText)
                    {
                        UserInfo = UserInfoResponse = JsonConvert.DeserializeObject<UserInfoResponse>(jsonText);
                        UserInfoJsonText = jsonText;
                    }
                    break;
                //                case "UserRank":
                case MessageCommand.ResUserRank:
                    SelfRankInfo = JsonConvert.DeserializeObject<UserRankInfo>(jsonText);
                    break;
                //                case "ScoreRanking":
                case MessageCommand.ResScoreRanking:
                    RankInfos = JsonConvert.DeserializeObject<UserRankInfo[]>(jsonText);
                    break;
                default:
                    Console.WriteLine(header.Name + " is unmanaged");
                    break;
                }
                OnRecieveMessage(this, new RecieveMessageArgs { Message = param, JsonText = jsonText });
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed MessageClient::RecieveMessage()" + e);
                return false;
            }
            return true;
        }
    }
}
