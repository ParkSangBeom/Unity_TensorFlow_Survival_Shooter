using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LitJson;
using System.Text;

public class JsonParser
{
    public class TensorData
    {
        public List<float> LsObserver = new List<float>();
        public int Action = 0;
        public float Reward = 0.0f;
    }

    public string GetActionJsonData(TensorData tensorData)
    {
        StringBuilder sb = new StringBuilder();
        JsonWriter jsonw = new JsonWriter(sb);
        jsonw.WriteObjectStart();
        {
            jsonw.WritePropertyName("type");
            jsonw.Write("action");

            jsonw.WritePropertyName("datalist");
            jsonw.WriteArrayStart();
            {
                jsonw.WriteObjectStart();
                {
                    List<float> lsData = tensorData.LsObserver;
                    for (int i = 0; i < lsData.Count; ++i)
                    {
                        jsonw.WritePropertyName("ob_" + i.ToString());
                        jsonw.Write(lsData[i]);
                    }
                }
                jsonw.WriteObjectEnd();
            }
            jsonw.WriteArrayEnd();
        }
        jsonw.WriteObjectEnd();

        return sb.ToString();
    }

    public string GetTrainJsonData(List<TensorData> lsTensorData, bool isEnd)
    {
        StringBuilder sb = new StringBuilder();
        JsonWriter jsonw = new JsonWriter(sb);
        jsonw.WriteObjectStart();
        {
            jsonw.WritePropertyName("type");
            jsonw.Write("train");

            jsonw.WritePropertyName("end");
            jsonw.Write(isEnd ? "1" : "0");

            jsonw.WritePropertyName("datalist");
            jsonw.WriteArrayStart();
            {
                for (int i = 0; i < lsTensorData.Count; ++i)
                {
                    jsonw.WriteObjectStart();
                    {
                        TensorData tensorData = lsTensorData[i];
                        for (int k = 0; k < tensorData.LsObserver.Count; ++k)
                        {
                            jsonw.WritePropertyName("ob_" + k.ToString());
                            jsonw.Write(tensorData.LsObserver[k]);
                        }

                        jsonw.WritePropertyName("action");
                        jsonw.Write(tensorData.Action);

                        jsonw.WritePropertyName("reward");
                        jsonw.Write(tensorData.Reward);
                    }
                    jsonw.WriteObjectEnd();
                }
            }
            jsonw.WriteArrayEnd();
        }
        jsonw.WriteObjectEnd();

        return sb.ToString();
    }

    public string GetResultData(string data)
    {
        JsonData jsonData = JsonMapper.ToObject(data);
        string result = jsonData["result"].ToString();
        return result;
        //if (result == "train_true")
        //{
        //    AgentStart();
        //}
        //else if (result == "action_true")
        //{
        //    _curData.Action = int.Parse(jsonData["value"].ToString());
        //    _state = State.Update;
        //}
    }

    public int GetActionData(string data)
    {
        JsonData jsonData = JsonMapper.ToObject(data);
        int value = int.Parse(jsonData["value"].ToString());
        return value;
    }
}
