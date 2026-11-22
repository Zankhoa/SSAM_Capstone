using SSAM_Server.DTOs.Documents;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ProcessContent: constraints block row mapping (ExtractConstraintRows, GetDoubleAttr, GetBoolAttr, GetStringAttr, TrimOrNull).</summary>
public class ProcessContentConstraintRowsTests : DocumentContentHelperTestBase
{
    private static List<ConstraintRowDto> Rows(string rowsJson) => ExtractOne(Constraints(rowsJson)).Constraints;

    private static ConstraintRowDto Row(string rowJson) => Assert.Single(Rows("[" + rowJson + "]"));

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Fully populated row -> Every field mapped
    [Fact]
    public void ConstraintRows_WithFullRow_MapsEveryField()
    {
        var row = Row("{\"field\":\"Password\",\"kind\":\"password\",\"required\":true,\"min\":8,\"max\":64,\"lettersAndDigits\":true,\"options\":[\"x\",\"y\"],\"message\":\"Too weak\"}");

        Assert.Equal("Password", row.Field);
        Assert.Equal("password", row.Kind);
        Assert.True(row.Required);
        Assert.Equal(8d, row.Min);
        Assert.Equal(64d, row.Max);
        Assert.True(row.LettersAndDigits);
        Assert.Equal(new[] { "x", "y" }, row.Options.ToArray());
        Assert.Equal("Too weak", row.Message);
    }

    // UTC02: Row with only field -> Defaults: kind "text", flags false, min/max null, options empty, message null
    [Fact]
    public void ConstraintRows_WithOnlyField_AppliesDefaults()
    {
        var row = Row("{\"field\":\"Name\"}");

        Assert.Equal("Name", row.Field);
        Assert.Equal("text", row.Kind);
        Assert.False(row.Required);
        Assert.Null(row.Min);
        Assert.Null(row.Max);
        Assert.False(row.LettersAndDigits);
        Assert.Empty(row.Options);
        Assert.Null(row.Message);
    }

    // UTC03: Multiple rows -> Order preserved
    [Fact]
    public void ConstraintRows_WithMultipleRows_PreservesOrder()
    {
        var rows = Rows("[{\"field\":\"A\"},{\"field\":\"B\"},{\"field\":\"C\"}]");

        Assert.Equal(new[] { "A", "B", "C" }, rows.Select(r => r.Field).ToArray());
    }

    // UTC04: Field and kind padded with whitespace -> Trimmed
    [Fact]
    public void ConstraintRows_WithPaddedFieldAndKind_Trims()
    {
        var row = Row("{\"field\":\"  Name  \",\"kind\":\"  number \"}");

        Assert.Equal("Name", row.Field);
        Assert.Equal("number", row.Kind);
    }

    // UTC05: options as array -> Items trimmed and kept
    [Fact]
    public void ConstraintRows_WithOptionsArray_TrimsItems()
    {
        var row = Row("{\"field\":\"F\",\"options\":[\" a \",\"b\"]}");

        Assert.Equal(new[] { "a", "b" }, row.Options.ToArray());
    }

    // UTC06: options as comma-separated string -> Split and trimmed
    [Fact]
    public void ConstraintRows_WithOptionsString_SplitsOnComma()
    {
        var row = Row("{\"field\":\"F\",\"options\":\"red, green ,blue\"}");

        Assert.Equal(new[] { "red", "green", "blue" }, row.Options.ToArray());
    }

    // UTC07: Min/Max as decimal numbers -> Preserved as double
    [Fact]
    public void ConstraintRows_WithDecimalMinMax_PreservesFraction()
    {
        var row = Row("{\"field\":\"F\",\"min\":1.5,\"max\":99.25}");

        Assert.Equal(1.5d, row.Min);
        Assert.Equal(99.25d, row.Max);
    }

    // UTC08: Min/Max as integer numbers -> Converted to double
    [Fact]
    public void ConstraintRows_WithIntegerMinMax_ConvertsToDouble()
    {
        var row = Row("{\"field\":\"F\",\"min\":0,\"max\":100}");

        Assert.Equal(0d, row.Min);
        Assert.Equal(100d, row.Max);
    }

    // UTC09: Min/Max as large integers beyond int range -> Converted to double
    [Fact]
    public void ConstraintRows_WithLongMinMax_ConvertsToDouble()
    {
        var row = Row("{\"field\":\"F\",\"min\":3000000000,\"max\":9000000000000}");

        Assert.Equal(3000000000d, row.Min);
        Assert.Equal(9000000000000d, row.Max);
    }

    // UTC10: Min/Max as numeric strings -> Parsed (invariant culture, trimmed)
    [Fact]
    public void ConstraintRows_WithNumericStringMinMax_Parsed()
    {
        var row = Row("{\"field\":\"F\",\"min\":\" 5 \",\"max\":\"1.5e2\"}");

        Assert.Equal(5d, row.Min);
        Assert.Equal(150d, row.Max);
    }

    // UTC11: Negative numbers -> Preserved
    [Fact]
    public void ConstraintRows_WithNegativeMinMax_Preserved()
    {
        var row = Row("{\"field\":\"F\",\"min\":-10,\"max\":\"-0.5\"}");

        Assert.Equal(-10d, row.Min);
        Assert.Equal(-0.5d, row.Max);
    }

    // UTC12: Boolean attrs true -> true (required and lettersAndDigits)
    [Fact]
    public void ConstraintRows_WithTrueBooleans_AreTrue()
    {
        var row = Row("{\"field\":\"F\",\"required\":true,\"lettersAndDigits\":true}");

        Assert.True(row.Required);
        Assert.True(row.LettersAndDigits);
    }

    // UTC13: Boolean attrs false -> false
    [Fact]
    public void ConstraintRows_WithFalseBooleans_AreFalse()
    {
        var row = Row("{\"field\":\"F\",\"required\":false,\"lettersAndDigits\":false}");

        Assert.False(row.Required);
        Assert.False(row.LettersAndDigits);
    }

    // UTC14: Constraint rows end up in lower-cased serialized hash part -> Hash differs when a row changes
    [Fact]
    public void ConstraintRows_ChangeInRow_ChangesContentHash()
    {
        var a = ExtractOne(Constraints("[{\"field\":\"A\",\"min\":1}]"));
        var b = ExtractOne(Constraints("[{\"field\":\"A\",\"min\":2}]"));

        Assert.NotEqual(a.ContentHash, b.ContentHash);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC15: Row with missing field -> Skipped
    [Fact]
    public void ConstraintRows_WithMissingField_Skipped()
    {
        Assert.Empty(Rows("[{\"kind\":\"text\"}]"));
    }

    // UTC16: Row with blank field -> Skipped
    [Fact]
    public void ConstraintRows_WithBlankField_Skipped()
    {
        var rows = Rows("[{\"field\":\"\"},{\"field\":\"   \"},{\"field\":\"Keep\"}]");

        Assert.Equal("Keep", Assert.Single(rows).Field);
    }

    // UTC17: Row with non-string field (number) -> Skipped
    [Fact]
    public void ConstraintRows_WithNonStringField_Skipped()
    {
        Assert.Empty(Rows("[{\"field\":5}]"));
    }

    // UTC18: Rows array contains non-object items -> Skipped
    [Fact]
    public void ConstraintRows_WithNonObjectItems_Skipped()
    {
        var rows = Rows("[\"x\",5,null,[1],{\"field\":\"Ok\"}]");

        Assert.Equal("Ok", Assert.Single(rows).Field);
    }

    // UTC19: attrs.rows is not an array (object) -> No rows
    [Fact]
    public void ConstraintRows_WhenRowsNotArray_NoRows()
    {
        Assert.Empty(ExtractOne("{\"type\":\"constraints\",\"attrs\":{\"rows\":{\"field\":\"A\"}}}").Constraints);
    }

    // UTC20: attrs.rows null -> No rows
    [Fact]
    public void ConstraintRows_WhenRowsNull_NoRows()
    {
        Assert.Empty(ExtractOne("{\"type\":\"constraints\",\"attrs\":{\"rows\":null}}").Constraints);
    }

    // UTC21: attrs is not an object -> No rows
    [Fact]
    public void ConstraintRows_WhenAttrsNotObject_NoRows()
    {
        Assert.Empty(ExtractOne("{\"type\":\"constraints\",\"attrs\":\"x\"}").Constraints);
    }

    // UTC22: Empty rows array -> No rows
    [Fact]
    public void ConstraintRows_WithEmptyRowsArray_NoRows()
    {
        Assert.Empty(Rows("[]"));
    }

    // UTC23: Blank kind -> Defaults to "text"
    [Fact]
    public void ConstraintRows_WithBlankKind_DefaultsToText()
    {
        var row = Row("{\"field\":\"F\",\"kind\":\"   \"}");

        Assert.Equal("text", row.Kind);
    }

    // UTC24: Non-string kind -> Defaults to "text"
    [Fact]
    public void ConstraintRows_WithNonStringKind_DefaultsToText()
    {
        var row = Row("{\"field\":\"F\",\"kind\":7}");

        Assert.Equal("text", row.Kind);
    }

    // UTC25: options array with blank and non-string entries -> Those entries skipped
    [Fact]
    public void ConstraintRows_WithBlankOptionEntries_SkipsThem()
    {
        var row = Row("{\"field\":\"F\",\"options\":[\"\",\"  \",5,null,\"ok\"]}");

        Assert.Equal(new[] { "ok" }, row.Options.ToArray());
    }

    // UTC26: options string with empty segments and whitespace -> Empty segments dropped
    [Fact]
    public void ConstraintRows_WithOptionsStringEmptySegments_DropsThem()
    {
        var row = Row("{\"field\":\"F\",\"options\":\",a,, ,b,\"}");

        Assert.Equal(new[] { "a", "b" }, row.Options.ToArray());
    }

    // UTC27: options is a blank string -> Empty list
    [Fact]
    public void ConstraintRows_WithBlankOptionsString_EmptyList()
    {
        var row = Row("{\"field\":\"F\",\"options\":\"   \"}");

        Assert.Empty(row.Options);
    }

    // UTC28: options is a number / object -> Empty list
    [Fact]
    public void ConstraintRows_WithNonStringNonArrayOptions_EmptyList()
    {
        var number = Row("{\"field\":\"F\",\"options\":5}");
        var obj = Row("{\"field\":\"F\",\"options\":{\"a\":1}}");

        Assert.Empty(number.Options);
        Assert.Empty(obj.Options);
    }

    // UTC29: Non-numeric string min/max -> null
    [Fact]
    public void ConstraintRows_WithNonNumericStringMinMax_Null()
    {
        var row = Row("{\"field\":\"F\",\"min\":\"abc\",\"max\":\"\"}");

        Assert.Null(row.Min);
        Assert.Null(row.Max);
    }

    // UTC30: String "NaN" / "Infinity" min/max -> null (rejected)
    [Fact]
    public void ConstraintRows_WithNaNOrInfinityStrings_Null()
    {
        var row = Row("{\"field\":\"F\",\"min\":\"NaN\",\"max\":\"Infinity\"}");

        Assert.Null(row.Min);
        Assert.Null(row.Max);
    }

    // UTC31: String "-Infinity" -> null (rejected)
    [Fact]
    public void ConstraintRows_WithNegativeInfinityString_Null()
    {
        var row = Row("{\"field\":\"F\",\"min\":\"-Infinity\"}");

        Assert.Null(row.Min);
    }

    // UTC32: min/max booleans, objects, arrays, null -> null
    [Fact]
    public void ConstraintRows_WithNonNumericTypesMinMax_Null()
    {
        var row = Row("{\"field\":\"F\",\"min\":true,\"max\":{\"a\":1}}");
        var row2 = Row("{\"field\":\"F\",\"min\":null,\"max\":[1]}");

        Assert.Null(row.Min);
        Assert.Null(row.Max);
        Assert.Null(row2.Min);
        Assert.Null(row2.Max);
    }

    // UTC33: String with comma decimal "1,5" -> Not parsed with invariant culture -> null (thousands not allowed)
    [Fact]
    public void ConstraintRows_WithCommaDecimalString_Null()
    {
        var row = Row("{\"field\":\"F\",\"min\":\"1,5\"}");

        Assert.Null(row.Min);
    }

    // UTC34: Boolean attr as string "true" -> false (non-bool)
    [Fact]
    public void ConstraintRows_WithStringBooleans_AreFalse()
    {
        var row = Row("{\"field\":\"F\",\"required\":\"true\",\"lettersAndDigits\":\"true\"}");

        Assert.False(row.Required);
        Assert.False(row.LettersAndDigits);
    }

    // UTC35: Boolean attr as number 1 / null -> false
    [Fact]
    public void ConstraintRows_WithNumberOrNullBooleans_AreFalse()
    {
        var row = Row("{\"field\":\"F\",\"required\":1,\"lettersAndDigits\":null}");

        Assert.False(row.Required);
        Assert.False(row.LettersAndDigits);
    }

    // UTC36: Blank message -> null
    [Fact]
    public void ConstraintRows_WithBlankMessage_Null()
    {
        var row = Row("{\"field\":\"F\",\"message\":\"   \"}");

        Assert.Null(row.Message);
    }

    // UTC37: Non-string message -> null
    [Fact]
    public void ConstraintRows_WithNonStringMessage_Null()
    {
        var row = Row("{\"field\":\"F\",\"message\":5}");

        Assert.Null(row.Message);
    }

    // UTC38: Message padded -> Trimmed
    [Fact]
    public void ConstraintRows_WithPaddedMessage_Trims()
    {
        var row = Row("{\"field\":\"F\",\"message\":\"  hi  \"}");

        Assert.Equal("hi", row.Message);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Field/option: 199, 200, 201 | Kind: 49, 50, 51 | Message: 499, 500, 501)
    // =========================================================================

    // UTC39: Field length 199 -> Preserved
    [Fact]
    public void ConstraintRows_WhenFieldLengthIs199_Preserved()
    {
        Assert.Equal(new string('f', 199), Row("{\"field\":\"" + new string('f', 199) + "\"}").Field);
    }

    // UTC40: Field length 200 -> Preserved
    [Fact]
    public void ConstraintRows_WhenFieldLengthIs200_Preserved()
    {
        Assert.Equal(new string('f', 200), Row("{\"field\":\"" + new string('f', 200) + "\"}").Field);
    }

    // UTC41: Field length 201 -> Truncated to 200
    [Fact]
    public void ConstraintRows_WhenFieldLengthIs201_TruncatedTo200()
    {
        Assert.Equal(new string('f', 200), Row("{\"field\":\"" + new string('f', 201) + "\"}").Field);
    }

    // UTC42: Kind length 49 -> Preserved
    [Fact]
    public void ConstraintRows_WhenKindLengthIs49_Preserved()
    {
        Assert.Equal(new string('k', 49), Row("{\"field\":\"F\",\"kind\":\"" + new string('k', 49) + "\"}").Kind);
    }

    // UTC43: Kind length 50 -> Preserved
    [Fact]
    public void ConstraintRows_WhenKindLengthIs50_Preserved()
    {
        Assert.Equal(new string('k', 50), Row("{\"field\":\"F\",\"kind\":\"" + new string('k', 50) + "\"}").Kind);
    }

    // UTC44: Kind length 51 -> Truncated to 50
    [Fact]
    public void ConstraintRows_WhenKindLengthIs51_TruncatedTo50()
    {
        Assert.Equal(new string('k', 50), Row("{\"field\":\"F\",\"kind\":\"" + new string('k', 51) + "\"}").Kind);
    }

    // UTC45: Option length 200 -> Preserved; 201 -> truncated to 200 (array form)
    [Fact]
    public void ConstraintRows_OptionLengthBoundaryInArray_TruncatesAbove200()
    {
        var row = Row("{\"field\":\"F\",\"options\":[\"" + new string('o', 199) + "\",\"" + new string('o', 200) + "\",\"" + new string('o', 201) + "\"]}");

        Assert.Equal(new[] { 199, 200, 200 }, row.Options.Select(o => o.Length).ToArray());
    }

    // UTC46: Option length 201 in comma string -> Truncated to 200
    [Fact]
    public void ConstraintRows_OptionLengthIs201InString_TruncatedTo200()
    {
        var row = Row("{\"field\":\"F\",\"options\":\"" + new string('o', 201) + ",b\"}");

        Assert.Equal(new[] { 200, 1 }, row.Options.Select(o => o.Length).ToArray());
    }

    // UTC47: Message length 499 -> Preserved
    [Fact]
    public void ConstraintRows_WhenMessageLengthIs499_Preserved()
    {
        Assert.Equal(new string('m', 499), Row("{\"field\":\"F\",\"message\":\"" + new string('m', 499) + "\"}").Message);
    }

    // UTC48: Message length 500 -> Preserved
    [Fact]
    public void ConstraintRows_WhenMessageLengthIs500_Preserved()
    {
        Assert.Equal(new string('m', 500), Row("{\"field\":\"F\",\"message\":\"" + new string('m', 500) + "\"}").Message);
    }

    // UTC49: Message length 501 -> Truncated to 500
    [Fact]
    public void ConstraintRows_WhenMessageLengthIs501_TruncatedTo500()
    {
        Assert.Equal(new string('m', 500), Row("{\"field\":\"F\",\"message\":\"" + new string('m', 501) + "\"}").Message);
    }

    // UTC50: Min/Max zero -> 0 (not null)
    [Fact]
    public void ConstraintRows_WithZeroMinMax_AreZeroNotNull()
    {
        var row = Row("{\"field\":\"F\",\"min\":0,\"max\":\"0\"}");

        Assert.Equal(0d, row.Min);
        Assert.Equal(0d, row.Max);
    }

    // UTC51: Very large double 1e308 -> Preserved; 1e309 string overflows to Infinity -> null
    [Fact]
    public void ConstraintRows_WithExtremeValues_HandlesOverflow()
    {
        var row = Row("{\"field\":\"F\",\"min\":1e308,\"max\":\"1e309\"}");

        Assert.Equal(1e308d, row.Min);
        Assert.Null(row.Max);
    }
}
