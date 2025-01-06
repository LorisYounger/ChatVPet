using Microsoft.ML;
using Microsoft.ML.Transforms.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 词向量引擎
    /// </summary>
    public class W2VEngine
    {
        public static MLContext MLContext = new MLContext();

        /// <summary>
        /// 是否需要更新
        /// </summary>
        public bool NeedUpdate { get; set; } = false;
        /// <summary>
        /// 是否是使用指定的模型
        /// </summary>
        public bool IsAppoint { get; set; } = false;
        /// <summary>
        /// 模型
        /// </summary>
        public ITransformer? Model { get; set; }


        /// <summary>
        /// 引索来源
        /// </summary>
        public VPetChatProcess CP { get; set; }

        public W2VEngine(VPetChatProcess vPetChatProcess)
        {
            CP = vPetChatProcess;
        }
        /// <summary>
        /// 临时用输入数据
        /// </summary>
        class InputData : Iw2vSource
        {
            public string KeyWords { get; set; }
            public float[]? Vector { get; set; }

            public InputData(VPetChatProcess vPetChatProcess, string text)
            {
                KeyWords = string.Join(" ", vPetChatProcess.Localization.WordSplit(text));
            }
            public InputData() { KeyWords = ""; }
        }
        /// <summary>
        /// 获得最新模型(如果需要更新会自动更新模型内容)
        /// </summary>
        /// <returns></returns>
        public ITransformer GetLatestModel()
        {
            lock (this)
                if (Model == null || (NeedUpdate && !IsAppoint))
                {
                    List<Iw2vSource> data =
                    [
                        .. CP.Dialogues,
                        .. CP.Tools,
                        .. CP.KnowledgeDataBases,
                        new InputData(CP, CP.SystemDescription),
                    ];
                    IDataView trainData = MLContext.Data.LoadFromEnumerable(data);

                    // 特征提取
                    // 创建自定义停用词移除器的选项
                    var stopWordsOptions = new CustomStopWordsRemovingEstimator.Options
                    {
                        StopWords = CP.Localization.StopWords // 设置自定义停用词列表
                    };
                    var options = new TextFeaturizingEstimator.Options()
                    {
                        WordFeatureExtractor = new WordBagEstimator.Options()
                        {
                            // 这里设置的是 1-gram, 2-gram 和 3-gram 的最大特征数量
                            //MaximumNgramsCount = [512, 256, 64],
                            //NgramLength = 3, // 提取 1-gram 和 2-gram
                            SkipLength = 0, // 不跳过任何词
                            Weighting = NgramExtractingEstimator.WeightingCriteria.TfIdf
                        },
                        KeepNumbers = false,
                        KeepDiacritics = false,
                        KeepPunctuations = false,
                        StopWordsRemoverOptions = stopWordsOptions
                    };

                    var pipeline = MLContext.Transforms.Text.FeaturizeText("Vector", options, nameof(Iw2vSource.KeyWords));
                    Model = pipeline.Fit(trainData);

                    //更新向量
                    GetQueryVector(data);
                    var queryDataView = MLContext.Data.LoadFromEnumerable(data.Select(x => new InputData() { KeyWords = x.KeyWords }));
                    var transformedQuery = GetLatestModel().Transform(queryDataView);
                    var documents = MLContext.Data.CreateEnumerable<InputData>(transformedQuery, reuseRowObject: false).ToList();
                    for (int i = 0; i < data.Count; i++)
                    {
                        data[i].Vector = documents[i].Vector;
                    }
                    NeedUpdate = false;
                }
            return Model;
        }

        /// <summary>
        /// 获得指定文本的向量
        /// </summary>
        public float[] GetQueryVector(string query)
        {
            var queryData = new[] { new InputData(CP, query) };
            var queryDataView = MLContext.Data.LoadFromEnumerable(queryData);
            var transformedQuery = GetLatestModel().Transform(queryDataView);
            var queryFeatures = MLContext.Data.CreateEnumerable<InputData>(transformedQuery, reuseRowObject: false).First();
            return queryFeatures.Vector ?? throw new Exception("Error Model Output");
        }
        /// <summary>
        /// 获得/更新 指定原列的向量
        /// </summary>
        /// <param name="iw2vSource"></param>
        public void GetQueryVector(IEnumerable<Iw2vSource> iw2vSource)
        {
            var needUpdate = iw2vSource.Where(x => x.Vector == null).ToList();
            if (!needUpdate.Any())
            {
                return;
            }
            var queryDataView = MLContext.Data.LoadFromEnumerable(needUpdate.Select(x => new InputData() { KeyWords = x.KeyWords }));
            var transformedQuery = GetLatestModel().Transform(queryDataView);
            var documents = MLContext.Data.CreateEnumerable<InputData>(transformedQuery, reuseRowObject: false).ToList();
            for (int i = 0; i < needUpdate.Count; i++)
            {
                needUpdate[i].Vector = documents[i].Vector;
            }
        }


        /// <summary>
        /// 计算余弦相似度
        /// </summary>
        public static float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0) return 0;
            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }


    }
}
