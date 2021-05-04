using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;

namespace FakeServer.Network
{
    // メッセージ送信タスク管理用ワーク
    public class SendMessageWork //: Task
    {
        //　Note:規模感に比べてTaskを継承するのはコストに見合わないのでやめています

        Task<bool> WorkTask;
        CancellationTokenSource CancellTokenSource = new CancellationTokenSource();

        //internal SendMessageWork(Action action) : this(action, new CancellationTokenSource())
        //{

        //}
        //internal SendMessageWork(Action action, CancellationTokenSource cancellTokenSource) : base(action, cancellTokenSource.Token)
        //{
        //    CancellTokenSource = cancellTokenSource;
        //}

        internal SendMessageWork(NetworkStream stream, byte[] header, byte[] body,Action<SendMessageWork> endCallback)
        {
            CancellTokenSource = new CancellationTokenSource();

            WorkTask = Task<bool>.Run(async () =>
            {
                try
                {
                    //  ヘッダー送信
                    await stream.WriteAsync(header, 0, header.Length);
                    //  本文を送信
                    await stream.WriteAsync(body, 0, body.Length);
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("SendMessage failed:" + e.Message);
                    //                    throw;
                    return false;
                }
            }, CancellTokenSource.Token).ContinueWith<bool>(t => {
                endCallback.Invoke(this);
                if(t.IsCompleted)
                {
                    return t.Result;
                }
                return false;
            });
        }

        public bool IsCompleted { get { return (WorkTask != null) ? WorkTask.IsCompleted : false; } }
        public bool IsCanceled { get { return (WorkTask != null) ? WorkTask.IsCanceled : false; } }

        public bool IsSuccess { get { return (IsCompleted) ? WorkTask.Result : false; } }

        public void Cancel()
        {
            if(CancellTokenSource != null)
            {
                CancellTokenSource.Cancel();
                CancellTokenSource = null;
            }
        }

    }
}
