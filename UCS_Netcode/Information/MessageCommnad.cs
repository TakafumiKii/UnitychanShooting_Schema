using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FakeServer.Network.Information
{
    //  送受信するメッセージのリスト
    //  通信仕様上15文字まで
    public enum MessageCommand
    {
        Login,
        ResUserInfo, 

        UploadUserScore,
        ResUserRank,

        GetScoreRanking,    
        ResScoreRanking,
    }
}
