// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KasaTapoClient.Internal;

internal static class JsonSupport
	{
	public static readonly JsonSerializerSettings COMPACT_JSON = CreateCompactJsonSettings ();

	public static JObject ParseObject (string json)
		{
		JToken? node = JToken.Parse (json);
		if (node is not JObject jsonObject)
			{
			throw new InvalidDataException ("The JSON payload was not an object.");
			}

		return jsonObject;
		}

	public static void MergeObjects (JObject target, JObject source)
		{
		foreach (KeyValuePair<string, JToken?> property in source)
			{
			if (target[property.Key] is JObject targetObject
				&& property.Value is JObject sourceObject)
				{
				MergeObjects (targetObject, sourceObject);
				continue;
				}

			target[property.Key] = property.Value?.DeepClone ();
			}
		}

	private static JsonSerializerSettings CreateCompactJsonSettings ()
		{
		var settings = new JsonSerializerSettings
			{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Include,
			};
		return settings;
		}
	}

internal static class JsonNodeExtensions
	{
	public static string ToJsonString (this JToken token, JsonSerializerSettings? settings = null)
		{
		return token.ToString (settings?.Formatting ?? Formatting.Indented);
		}

	public static T? GetValue<T> (this JToken token)
		{
		return token.ToObject<T> ();
		}
	}

internal sealed class NullableFlexibleInt32Converter : JsonConverter<int?>
	{
	public override int? ReadJson (JsonReader reader, Type objectType, int? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
		return reader.TokenType switch
			{
				JsonToken.Null => null,
				JsonToken.Integer => Convert.ToInt32 (reader.Value, CultureInfo.InvariantCulture),
				JsonToken.Float => TryConvertDoubleToInt32 (Convert.ToDouble (reader.Value, CultureInfo.InvariantCulture)),
				JsonToken.String => TryParseFlexibleInt32 (reader.Value as string),
				JsonToken.Boolean => (bool)reader.Value! ? 1 : 0,
				_ => null,
			};
		}

	public override void WriteJson (JsonWriter writer, int? value, JsonSerializer serializer)
		{
		if (value is int intValue)
			{
			writer.WriteValue (intValue);
			return;
			}

		writer.WriteNull ();
		}

	private static int? TryParseFlexibleInt32 (string? value)
		{
		if (string.IsNullOrWhiteSpace (value))
			{
			return null;
			}

		if (int.TryParse (value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
			{
			return parsed;
			}

		if (double.TryParse (value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
			{
			return TryConvertDoubleToInt32 (doubleValue);
			}

		return null;
		}

	private static int? TryConvertDoubleToInt32 (double doubleValue)
		{
		return !double.IsNaN (doubleValue) && !double.IsInfinity (doubleValue) && doubleValue >= int.MinValue && doubleValue <= int.MaxValue
			? (int)Math.Round (doubleValue, MidpointRounding.AwayFromZero)
			: null;
		}
	}
