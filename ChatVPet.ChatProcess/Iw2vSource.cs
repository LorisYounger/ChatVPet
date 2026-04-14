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
        /// 关键字组 (集合)
        /// </summary>
        IEnumerable<string> KeyWords { get; }

        /// <summary>
        /// 向量 (集合)
        /// </summary>
        float[]? Vector { get; set; }
    }
}
