using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace FakeServer.Unity
{
    using Netcode.Message;
    using Netcode.Scheme;
    public class NetworkClient : MonoBehaviour
    {
        [SerializeField] string IPAddress = "localhost";
        [SerializeField] int PortNo = 40080;

        MessageClient Client = new MessageClient();
        public static readonly string USER_INFO_PATH = "userinfo.json";// TODO:余裕があれば保存先の隠蔽や暗号化を行いたい

        public UserInfo UserInfo { get; private set; }

        public UserInfoResponse UserInfoResponse { get { return Client.UserInfoResponse; } }
        public UserRankInfo SelfRankInfo { get { return Client.SelfRankInfo; } }
        public UserRankInfo[] RankInfos { get { return Client.RankInfos; } }

        public bool IsConnected { get { return Client.IsActive; } }
        public bool IsLogin { get { return (Client.UserInfo != null); } }

        public bool IsCompleteSendMessage { get; private set; }

        bool WaitRecieveUserInfo;
        bool WaitRecieveScoreRanking;
        bool WaitRecieveUserRank;


        private void Awake()
        {
            LoadUserInfo(USER_INFO_PATH);
        }
        // Use this for initialization
        void Start () {
        }

        private void OnDestroy()
        {
        }
        private void OnEnable()
        {
            Client.OnRecieveMessage += RecieveMessage;
            Client.OnChangeState += OnChangeState;
        }
        private void OnDisable()
        {
            Client.OnChangeState -= OnChangeState;
            Client.OnRecieveMessage -= RecieveMessage;
            Terminate();
        }


        // Update is called once per frame
        void Update () {
        }

        void Terminate()
        {
            if(Client.IsInitialized)
            {
                StopCoroutine(Login());
                Client.Close();
            }
            WaitRecieveUserInfo = false;
            WaitRecieveScoreRanking = false;
            WaitRecieveUserRank = false;
        }



        public IEnumerator Login()
        {
            IsCompleteSendMessage = false;
            if (Client.IsInitialized == false)
            {
                var task = Client.Connect(IPAddress, PortNo);
                if (task == null)
                {// 接続タスク　終了
                    yield break;
                }
            }
            Debug.Log("LoginTask connecting");

            while (!Client.IsActive)
            {
                if(Client.IsInitialized)
                {// 初期化待ち
                    yield return null;
                }
                else
                {// 内部エラー発生
                    Debug.Log("Abort ConnectTask");
                    yield break;
                }
            }
            Debug.Log("LoginTask connected");

            WaitRecieveUserInfo = true;
            Client.Login(UserInfo);

            while(WaitRecieveUserInfo)
            {
                if(!Client.IsActive)
                {
                    Debug.Log("Abort ConnectTask WaitRecieveUserInfo");
                    yield break;
                }
                //  ログイン情報を取得するまで待機
                yield return null;
            }
            IsCompleteSendMessage = true;
        }

        public IEnumerator UploadUserScore(int point)
        {
            IsCompleteSendMessage = false;
            if (!Client.IsActive)
            {
                yield break;
            }

            WaitRecieveUserRank = true;
            Client.UploadScore(point);

            while(WaitRecieveUserRank)
            {
                if (!Client.IsActive)
                {
                    Debug.Log("Abort UploadUserScore WaitRecieveUserRank");
                    yield break;
                }
                yield return null;
            }
            IsCompleteSendMessage = true;
        }

        public IEnumerator RequestRanking(int skip,int take,bool isSelf = false)
        {
            IsCompleteSendMessage = false;
            if (!Client.IsActive)
            {
                yield break;
            }

            WaitRecieveScoreRanking = true;

            Client.RequestScoreRanking(skip, take,(isSelf)? UserInfo:null);
            while (WaitRecieveScoreRanking)
            {
                if (!Client.IsActive)
                {
                    Debug.Log("Abort RequestRanking WaitRecieveScoreRanking");
                    yield break;
                }
                yield return null;
            }
            IsCompleteSendMessage = true;
        }

        bool LoadUserInfo(string filePath)
        {
            if (File.Exists(filePath))  // TODO:ファイルパスを変更可能にする
            {
                // JSON
                StreamReader sr = File.OpenText(filePath);
                string jsonText = sr.ReadToEnd();
                UserInfo = JsonConvert.DeserializeObject<UserInfo>(jsonText);
                sr.Close();
                return true;
            }
            else
            {
                UserInfo = new UserInfo();
                return false;
            }
        }
        void SaveUserInfo(string filePath, string jsontext)
        {
            StreamWriter sw = File.CreateText(filePath);
            sw.Write(jsontext);
            sw.Close();
            Console.WriteLine("Save UserInfo");
        }

        public void ResetUserInfo()
        {
            string filePath = USER_INFO_PATH;
            if (File.Exists(filePath))  // TODO:ファイルパスを変更可能にする
            {
                File.Delete(filePath);
            }
            UserInfo = new UserInfo();
        }

        void RecieveMessage(object sender, MessageClient.RecieveMessageArgs args)
        {
            Debug.Log("Recieve " + args.Message.ToString());

            switch (args.Message)
            {
            //            case "UserInfoRespon":
            case MessageCommand.ResUserInfo:
                if (UserInfo.IsSame(Client.UserInfo) == false)
                {// 受信したデータが違った
                    UserInfo.Copy(Client.UserInfo);

                    // 余計な情報を保存したくないのでjsonTextを作り直す
                    string text = JsonConvert.SerializeObject(UserInfo);
                    StreamWriter sw = File.CreateText(USER_INFO_PATH);
                    sw.Write(text);
                    sw.Close();
                    Console.WriteLine("Save UserInfo");
                }
                WaitRecieveUserInfo = false;
                break;
//            case "UserRank":
            case MessageCommand.ResUserRank:
                WaitRecieveUserRank = false;
                break;
//            case "ScoreRanking":
            case MessageCommand.ResScoreRanking:
                WaitRecieveScoreRanking = false;
                break;
            }
        }

        void OnChangeState(object sender, MessageClient.ChangeStateArg arg)
        {

        }

    }

}
