// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KasaTapoClient.Internal;

internal static class WireJson
	{
	internal static readonly JsonSerializerOptions Options = new ()
		{
		NumberHandling = JsonNumberHandling.AllowReadingFromString,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};

	internal static T? Deserialize<T> (string json) => JsonSerializer.Deserialize<T> (json,Options);
	internal static T Read<T> (string json) where T : class => Deserialize<T> (json)
		?? throw new InvalidDataException ("The device returned a null JSON payload.");
	internal static string Serialize<T> (T value) => JsonSerializer.Serialize (value,Options);
	}

// Some sensor firmware encodes measurements as strings, fractions or booleans.
internal sealed class FlexibleNullableInt32Converter : JsonConverter<int?>
	{
	public override int? Read (ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options)
		{
		switch (reader.TokenType)
			{
			case JsonTokenType.Null: return null;
			case JsonTokenType.True: return 1;
			case JsonTokenType.False: return 0;
			case JsonTokenType.Number:
				return reader.TryGetInt32 (out int integer) ? integer : Round (reader.GetDouble ());
			case JsonTokenType.String:
				return double.TryParse (reader.GetString (),NumberStyles.Float,CultureInfo.InvariantCulture,out double number) ? Round (number) : null;
			default:
				reader.Skip ();
				return null;
			}
		}

	public override void Write (Utf8JsonWriter writer,int? value,JsonSerializerOptions options)
		{
		if (value is int number) writer.WriteNumberValue (number);
		else writer.WriteNullValue ();
		}

	private static int? Round (double number)
		{
		double rounded = Math.Round (number,MidpointRounding.AwayFromZero);
		return !double.IsNaN (rounded) && rounded >= int.MinValue && rounded <= int.MaxValue ? (int)rounded : null;
		}
	}
