using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    public interface Iw2vSource
    {
        /// <summary>
        /// 关键字组
        /// </summary>
        string KeyWords { get; set; }

        /// <summary>
        /// 向量
        /// </summary>
        float[]? Vector { get; set; }
    }
}
