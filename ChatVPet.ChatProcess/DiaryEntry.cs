namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 日记条目：对一段历史对话的摘要，继承 Dialogue，附带日期、权重衰减与命中计数机制。
    /// </summary>
    public class DiaryEntry : Dialogue
    {
        /// <summary>衰减下限，防止权重归零</summary>
        public const float MinDecayFactor = 0.05f;
        /// <summary>每次命中时衰减因子的恢复量</summary>
        public const float DecayRecoveryAmount = 0.1f;
        /// <summary>ImportanceWeight_Muti 的最低有效值</summary>
        public const float MinImportanceWeight = 0.1f;

        private static int _idCounter = 0;

        /// <summary>
        /// 唯一 ID（全局自增整数，以十六进制字符串表示，方便 AI 引用）
        /// </summary>
        public int Id { get; set; } = System.Threading.Interlocked.Increment(ref _idCounter);

        /// <summary>
        /// 反序列化后同步静态计数器，确保新建条目的 ID 不与已加载的条目重复。
        /// 将计数器置为不低于 <paramref name="value"/>，
        /// 之后 <see cref="System.Threading.Interlocked.Increment"/> 返回的值将严格大于 <paramref name="value"/>。
        /// </summary>
        public static void EnsureCounterAbove(int value)
        {
            int current;
            do
            {
                current = _idCounter;
                if (current >= value) break;
            }
            while (System.Threading.Interlocked.CompareExchange(ref _idCounter, value, current) != current);
        }

        /// <summary>
        /// 日记记录时间
        /// </summary>
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 基础重要性乘法权重（由 <see cref="VPetChatProcess.CalImportanceFunction"/> 初始化，LLM 可通过 toolcall 调节）
        /// </summary>
        public float BaseImportanceWeight { get; set; } = 2f;

        /// <summary>
        /// 被检索/提及的累计次数（命中次数越多权重越高）
        /// </summary>
        public int HitCount { get; set; } = 0;

        /// <summary>
        /// 衰减因子（每轮对话未被使用则减小；被命中则恢复）
        /// 范围：[<see cref="MinDecayFactor"/>, 1]，初始为 1.0
        /// </summary>
        public float DecayFactor { get; set; } = 1.0f;

        /// <summary>
        /// 无参构造（反序列化用）
        /// </summary>
        public DiaryEntry() : base() { }

        /// <summary>
        /// 创建日记条目
        /// </summary>
        /// <param name="summary">日记摘要文本</param>
        /// <param name="localization">本地化实例</param>
        /// <param name="baseImportanceWeight">基础重要性权重（由调用方通过 CalImportanceFunction 计算后传入）</param>
        /// <param name="recordedAt">记录时间（null 取当前时间）</param>
        public DiaryEntry(string summary, ILocalization localization, float baseImportanceWeight = 2f, DateTime? recordedAt = null)
            : base("", summary, "", localization)
        {
            RecordedAt = recordedAt ?? DateTime.Now;
            BaseImportanceWeight = baseImportanceWeight;
            RecalculateWeight();
        }

        /// <summary>
        /// 应用一次衰减（未被命中时调用）
        /// </summary>
        /// <param name="decayRate">每次衰减的比例，例如 0.05 表示衰减 5%</param>
        public void ApplyDecay(float decayRate)
        {
            DecayFactor = Math.Max(MinDecayFactor, DecayFactor * (1f - decayRate));
            RecalculateWeight();
        }

        /// <summary>
        /// 记录一次命中（被检索或被 LLM 主动提及）
        /// </summary>
        public void OnHit()
        {
            HitCount++;
            // 命中后恢复部分衰减，但不超过 1.0
            DecayFactor = Math.Min(1.0f, DecayFactor + DecayRecoveryAmount);
            RecalculateWeight();
        }

        /// <summary>
        /// 根据基础权重与衰减因子重新计算 ImportanceWeight_Muti
        /// </summary>
        public void RecalculateWeight()
        {
            ImportanceWeight_Muti = Math.Max(MinImportanceWeight, BaseImportanceWeight * DecayFactor);
        }

        /// <summary>
        /// 设置基础重要性权重（LLM 调节接口）
        /// </summary>
        public void SetBaseWeight(float weight)
        {
            BaseImportanceWeight = Math.Max(MinImportanceWeight, weight);
            RecalculateWeight();
        }

        /// <summary>
        /// 相对调整基础重要性权重（LLM 调节接口）
        /// </summary>
        public void AdjustBaseWeight(float delta)
        {
            SetBaseWeight(BaseImportanceWeight + delta);
        }

        /// <summary>
        /// 转换为在系统提示中展示的单行摘要（含十六进制 ID 和时间戳）
        /// </summary>
        public string ToContextLine()
        {
            return $"[{RecordedAt:yyyy-MM-dd HH:mm} #{Id:x}] {NormalizeContextText(Answer)}";
        }

        private static string NormalizeContextText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
