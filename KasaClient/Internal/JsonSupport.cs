// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace KasaTapoClient.Internal;

internal static class JsonSupport
	{
	public static readonly JsonSerializerOptions COMPACT_JSON = CreateCompactJsonOptions ();

	public static JsonObject ParseObject (string json)
		{
		JsonNode? node = JsonNode.Parse (json);
		if (node is not JsonObject jsonObject)
			{
			throw new InvalidDataException ("The JSON payload was not an object.");
			}

		return jsonObject;
		}

	public static void MergeObjects (JsonObject target, JsonObject source)
		{
		foreach (KeyValuePair<string, JsonNode?> property in source)
			{
			if (target[property.Key] is JsonObject targetObject
				&& property.Value is JsonObject sourceObject)
				{
				MergeObjects (targetObject, sourceObject);
				continue;
				}

			target[property.Key] = property.Value?.DeepClone ();
			}
		}

	private static JsonSerializerOptions CreateCompactJsonOptions ()
		{
		var options = new JsonSerializerOptions
			{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			PropertyNamingPolicy = null,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			};
		return options;
		}
	}

internal sealed class NullableFlexibleInt32Converter : JsonConverter<int?>
	{
	public override int? Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		return reader.TokenType switch
			{
				JsonTokenType.Null => null,
				JsonTokenType.Number => reader.TryGetInt32 (out int intValue)
					? intValue
					: (int?)null,
				JsonTokenType.String => TryParseFlexibleInt32 (reader.GetString ()),
				JsonTokenType.True => 1,
				JsonTokenType.False => 0,
				_ => SkipUnsupportedValue (ref reader),
			};
		}

	public override void Write (Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
		{
		if (value is int intValue)
			{
			writer.WriteNumberValue (intValue);
			return;
			}

		writer.WriteNullValue ();
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
			return !double.IsNaN (doubleValue) && !double.IsInfinity (doubleValue) && doubleValue >= int.MinValue && doubleValue <= int.MaxValue
				? (int)Math.Round (doubleValue, MidpointRounding.AwayFromZero)
				: null;
			}

		return null;
		}

	private static int? SkipUnsupportedValue (ref Utf8JsonReader reader)
		{
		reader.Skip ();
		return null;
		}
	}
