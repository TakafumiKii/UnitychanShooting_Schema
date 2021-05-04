using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FakeServer.Network.Information
{
    [JsonObject("user_info")]
    public class UserInfo
    {
        [JsonProperty("sereal")]
        public int Sereal;          //　新規ユーザーは0、二度目以降は保存された値が入ります
        [JsonProperty("name")]
        public string Name = "";

        public bool IsFirstTime { get { return (Sereal != 0); } }

        public bool IsSame(UserInfo info)
        {
            if (Sereal != info.Sereal) return false;
            if (Name != info.Name) return false;
            return true;
        }
        public void Copy(UserInfo info)
        {
            Sereal = info.Sereal;
            Name = info.Name;
        }
    }
    // サーバーからの返答用
    [JsonObject("user_info_response")]
    public class UserInfoResponse : UserInfo
    {
        [JsonProperty("user_id")]
        public int UserId = 0;      //  サーバー側で設定される識別値
        //            if (UserId != info.UserId) return false;
        public UserInfoResponse()
        {

        }
        public UserInfoResponse(UserInfo info,int userId)
        {
            Copy(info);
            UserId = userId;
        }
    }

    [JsonObject("user_score_info")]
    public class UserRankInfo
    {
        [JsonProperty("user_id")]
        public int UserId;
        [JsonProperty("name")]
        public string Name;// = "Nameless";
        [JsonProperty("point")]
        public int Point;
        [JsonProperty("rank")]
        public int Rank = 0;
        public bool IsSame(UserRankInfo info)
        {
            if (UserId != info.UserId) return false;
            if (Name != info.Name) return false;
            if (Point != info.Point) return false;
            if (Rank != info.Rank) return false;
            return true;
        }
    }

    // ポイント送信用
    [JsonObject("user_score_param")]
    public class UserScoreParam
    {
        [JsonProperty("sereal")]
        public int Sereal;
        [JsonProperty("point")]
        public int Point;
    }

    [JsonObject("ranking_req")]
    public class RankingRequest
    {
        [JsonProperty("skip")]
        public int Skip = 0;    //  取得開始順位(0の時に1位から開始)
        [JsonProperty("take")]
        public int Take = 10;   //  取得するレコード数
        [JsonProperty("sereal")]
        public int Sereal = 0;  //  ユーザー限定したい時用
    }
}
