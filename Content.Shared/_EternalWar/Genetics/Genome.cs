using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Genetics;

/// <summary>
/// Blocks of the genome, in the order the scanner shows them.
/// Colors take three blocks each: R, G and B.
/// </summary>
public enum GenomeBlock : byte
{
    Sentience,
    Race,
    Sex,
    SkinR,
    SkinG,
    SkinB,
    EyeR,
    EyeG,
    EyeB,
    RightEyeR,
    RightEyeG,
    RightEyeB,
    HairR,
    HairG,
    HairB,
    BeardR,
    BeardG,
    BeardB,
}

/// <summary>
/// Limits, thresholds and text form of the genome.
/// All values are written in hex, the way the scanner shows them.
/// </summary>
public static class Genome
{
    public const int BlockCount = 18;

    /// <summary>
    /// Below this the creature can't talk and makes monkey noises instead.
    /// </summary>
    public const int SentienceThreshold = 0x200;
    public const int SentienceMax = 0xFFF;

    public const int HumanRaceMin = 0x100;
    public const int RaceMax = 0x200;

    public const int FemaleMax = 0x534;
    public const int Male = 0x535;
    public const int SexMax = 0x800;

    public const int ColorMax = 0xFF;

    /// <summary>
    /// The first block of each color, in scanner order.
    /// </summary>
    public static readonly GenomeBlock[] Colors =
    [
        GenomeBlock.SkinR,
        GenomeBlock.EyeR,
        GenomeBlock.RightEyeR,
        GenomeBlock.HairR,
        GenomeBlock.BeardR,
    ];

    public static int MaxValue(GenomeBlock block)
    {
        return block switch
        {
            GenomeBlock.Sentience => SentienceMax,
            GenomeBlock.Race => RaceMax,
            GenomeBlock.Sex => SexMax,
            _ => ColorMax,
        };
    }

    public static int Digits(GenomeBlock block)
    {
        return block <= GenomeBlock.Sex ? 3 : 2;
    }

    public static string Format(GenomeBlock block, int value)
    {
        return value.ToString("X" + Digits(block), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads one block typed by hand. Case doesn't matter, leading zeros are optional.
    /// </summary>
    public static bool TryParse(GenomeBlock block, string text, out int value)
    {
        value = 0;
        text = text.Trim();

        if (text.Length == 0 || text.Length > Digits(block))
            return false;

        foreach (var c in text)
        {
            if (!IsHexDigit(c))
                return false;
        }

        return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
               && value <= MaxValue(block);
    }

    public static bool IsHexDigit(char c)
    {
        return c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
    }

    /// <summary>
    /// The whole genome as one line: <c>2A1-150-535-E0AC69-...</c>, colors written as RRGGBB.
    /// </summary>
    public static string FormatCode(int[] blocks)
    {
        var parts = new List<string>();

        for (var i = GenomeBlock.Sentience; i <= GenomeBlock.Sex; i++)
        {
            parts.Add(Format(i, blocks[(int) i]));
        }

        foreach (var first in Colors)
        {
            parts.Add(Format(first, blocks[(int) first])
                      + Format(first + 1, blocks[(int) first + 1])
                      + Format(first + 2, blocks[(int) first + 2]));
        }

        return string.Join("-", parts);
    }

    public static bool IsSentient(int value)
    {
        return value >= SentienceThreshold;
    }

    public static Sex GetSex(int value)
    {
        if (value <= FemaleMax)
            return Sex.Female;

        return value == Male ? Sex.Male : Sex.Unsexed;
    }

    public static Color GetColor(int[] blocks, GenomeBlock first)
    {
        var i = (int) first;
        return new Color(blocks[i] / 255f, blocks[i + 1] / 255f, blocks[i + 2] / 255f);
    }

    public static void SetColor(int[] blocks, GenomeBlock first, Color color)
    {
        var i = (int) first;
        blocks[i] = ToByte(color.R);
        blocks[i + 1] = ToByte(color.G);
        blocks[i + 2] = ToByte(color.B);
    }

    private static int ToByte(float channel)
    {
        return Math.Clamp((int) MathF.Round(channel * 255f), 0, 255);
    }

    /// <summary>
    /// Finds the race whose range holds this value of the race block.
    /// </summary>
    public static bool TryGetRace(IPrototypeManager protoMan, int value, [NotNullWhen(true)] out GenomeRacePrototype? race)
    {
        foreach (var proto in protoMan.EnumeratePrototypes<GenomeRacePrototype>())
        {
            if (proto.Contains(value))
            {
                race = proto;
                return true;
            }
        }

        race = null;
        return false;
    }
}
