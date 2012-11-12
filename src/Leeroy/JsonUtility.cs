using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Leeroy
{
	public static class JsonUtility
	{
		public static T FromJson<T>(string json)
		{
			return (T) FromJson(json, typeof(T));
		}

		public static object FromJson(string json, Type type)
		{
			using (StringReader reader = new StringReader(json))
				return FromJsonTextReader(reader, type);
		}

		public static T FromJsonTextReader<T>(TextReader textReader)
		{
			return (T) FromJsonTextReader(textReader, typeof(T));
		}

		public static object FromJsonTextReader(TextReader textReader, Type type)
		{
			try
			{
				using (JsonReader reader = new JsonTextReader(textReader))
					return Deserialize(reader, type);
			}
			catch (JsonReaderException ex)
			{
				throw new FormatException("Error reading JSON.", ex);
			}
			catch (JsonSerializationException ex)
			{
				throw new FormatException("Error deserializing JSON.", ex);
			}
		}

		public static string ToJson<T>(T value)
		{
			JsonSerializerSettings settings =
				new JsonSerializerSettings
				{
					ContractResolver = new CamelCasePropertyNamesContractResolver(),
					DateParseHandling = DateParseHandling.None,
					NullValueHandling = NullValueHandling.Ignore,
					MissingMemberHandling = MissingMemberHandling.Ignore,
				};

			JsonSerializer serializer = JsonSerializer.Create(settings);
			using (StringWriter writer = new StringWriter())
			{
				serializer.Serialize(writer, value);
				return writer.ToString();
			}
		}

		private static object Deserialize(JsonReader reader, Type type)
		{
			JsonSerializerSettings settings =
				new JsonSerializerSettings
				{
					ContractResolver = new CamelCasePropertyNamesContractResolver(),
					DateParseHandling = DateParseHandling.None,
					NullValueHandling = NullValueHandling.Ignore,
					MissingMemberHandling = MissingMemberHandling.Ignore,
				};

			JsonSerializer serializer = JsonSerializer.Create(settings);
			object value = serializer.Deserialize(reader, type);
			if (reader.Read() && reader.TokenType != JsonToken.Comment)
				throw new JsonSerializationException("Additional text found in JSON after deserializing.");
			if (value == null && type == typeof(JToken))
				value = new JValue((object) null);
			return value;
		}
	}
}
