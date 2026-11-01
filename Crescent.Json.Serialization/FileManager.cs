using Newtonsoft.Json;

namespace Crescent.Json.Serialization
{
    public static class FileManager
    { 
        public static JsonSerializerSettings? jsonSerializerSettings;

        public static JsonSerializerSettings GetJsonSerializerSettings()
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented
                };

            return jsonSerializerSettings;
        }

        #region Json Manipulation

        public static async Task<T> ReadJson<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Log($"File {filePath} does not exist!");
                return default!;
            }

            if (typeof(T).IsAbstract)
            {
                Logger.Log($"Cannot deserialize abstract type {typeof(T).FullName}!");
                return default!;
            }

            try
            {
                string text = await File.ReadAllTextAsync(filePath);
                if (!string.IsNullOrEmpty(text))
                {
                    T? obj = JsonConvert.DeserializeObject<T>(text, GetJsonSerializerSettings());
                    if (obj != null) return obj;
                }
            }
            catch (Exception exception)
            {
                Logger.Log($"Caught exception deserializing file at {filePath}:\n\n{exception.Message}\n{exception.StackTrace}");
            }

            return default!;
        }

        public static async Task WriteJson<T>(string filePath, T obj)
        {
            if (typeof(T).IsAbstract)
            {
                Logger.Log($"Cannot serialize abstract type {typeof(T).FullName}!");
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(obj, GetJsonSerializerSettings());
                if (!string.IsNullOrEmpty(json)) await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception exception)
            {
                Logger.Log($"Caught exception serializing file at {filePath}\n\n{exception.Message}\n{exception.StackTrace}");
            }
        }

        public static async Task<object?> ReadJson(Type type, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Log($"File {filePath} does not exist!");
                return null;
            }

            if (typeof(object).IsAbstract)
            {
                Logger.Log($"Cannot deserialize abstract type {type.FullName}!");
                return null;
            }

            try
            {
                string text = await File.ReadAllTextAsync(filePath);
                if (!string.IsNullOrEmpty(text)) return JsonConvert.DeserializeObject(text, type, GetJsonSerializerSettings());
            }
            catch (Exception exception)
            {
                Logger.Log($"Caught exception deserializing file at {filePath}:\n\n{exception.Message}\n{exception.StackTrace}");
            }

            return null;
        }

        public static void DeleteJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Log($"File {filePath} does not exist!");
                return;
            }

            File.Delete(filePath);
        }

        #endregion
    }
}