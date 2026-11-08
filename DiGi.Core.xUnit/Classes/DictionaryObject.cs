using DiGi.Core.Classes;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.Core.xUnit
{
    internal sealed class DictionaryObject : GuidObject
    {
        [JsonInclude, JsonPropertyName("Dictionary")]
        private Dictionary<string, TestObject>? dictionary;

        public DictionaryObject()
            : base()
        {
        }

        public DictionaryObject(JsonObject? jsonObject)
            : base(jsonObject)
        {
        }

        [JsonIgnore]
        public IEnumerable<string>? Values
        {
            get
            {
                if (dictionary == null)
                {
                    return null;
                }

                return [.. dictionary.Keys];
            }
        }

        public bool Add(string name)
        {
            if (name == null)
            {
                return false;
            }

            if (dictionary == null)
            {
                dictionary = new Dictionary<string, TestObject>();
            }

            dictionary[name] = new TestObject(name);
            return true;
        }
    }
}