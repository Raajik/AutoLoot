namespace AutoLoot.Loot;

/// <summary>
/// A loot rule requirement that compares a numeric property on an item to a target value.
///
/// In plain English: "Does this item's [PropType.PropKey] [Type] [TargetValue]?"
///
/// For example:
///   PropType    = ValueProp.PropertyInt
///   PropKey     = (int)PropertyInt.ArmorLevel   (= 29)
///   Type        = CompareType.GreaterThanEqual
///   TargetValue = 140
///   → Matches items where ArmorLevel >= 140
///
/// How it works:
///   1. GetProperty is called on the item using the specified type and key.
///   2. The result (which might be null if the property doesn't exist) is converted
///      to a nullable double via the Normalize() extension method.
///   3. The normalized value is compared to TargetValue using the chosen CompareType.
///
/// Unlike StringRequirement, ValueRequirement doesn't need Initialize() — it's ready to use
/// immediately after construction.
/// </summary>
public class ValueRequirement
{
    /// <summary>
    /// Which family of property type to look up (int, float, bool, etc.)
    /// This tells VerifyRequirement which typed GetProperty overload to call.
    /// </summary>
    public ValueProp PropType { get; set; }

    /// <summary>
    /// The integer value of the property enum key to look up.
    /// Cast from the appropriate ACE property enum, e.g.: (int)PropertyInt.ArmorLevel
    ///
    /// Stored as int (rather than the specific enum type) so we can handle all
    /// property types uniformly in one class.
    /// </summary>
    public int PropKey { get; set; }

    /// <summary>
    /// The value to compare the item's property against.
    ///
    /// Stored as double so it can hold int, float, bool (0/1), and other numeric types.
    /// This mirrors how ACE's RecipeManager normalizes property values for comparison.
    /// </summary>
    public double TargetValue { get; set; }

    /// <summary>
    /// How to compare the item's property value against TargetValue.
    /// See the CompareType enum (in PatchClass.cs) for all available options.
    /// </summary>
    public CompareType Type { get; set; }

    /// <summary>
    /// Looks up the item's property value, converts it to a nullable double, and runs the comparison.
    ///
    /// The PropType switch determines which typed GetProperty overload to call.
    /// After the lookup, Normalize() converts the result to double? (preserving null).
    /// Then the double? value is passed to the overload below for the actual comparison.
    ///
    /// Throws NotImplementedException if PropType has an unhandled value (shouldn't happen
    /// with the current enum, but good to have as a safety net).
    /// </summary>
    public bool VerifyRequirement(WorldObject item)
    {
        // Look up the property value based on which type family it belongs to,
        // then normalize it to a nullable double for uniform comparison.
        var normalizedValue = PropType switch
        {
            ValueProp.PropertyBool       => item.GetProperty((PropertyBool)PropKey).Normalize(),
            ValueProp.PropertyDataId     => item.GetProperty((PropertyDataId)PropKey).Normalize(),
            ValueProp.PropertyDouble     => item.GetProperty((PropertyFloat)PropKey).Normalize(),
            ValueProp.PropertyInstanceId => item.GetProperty((PropertyInstanceId)PropKey).Normalize(),
            ValueProp.PropertyInt        => item.GetProperty((PropertyInt)PropKey).Normalize(),
            ValueProp.PropertyInt64      => item.GetProperty((PropertyInt64)PropKey).Normalize(),
            _ => throw new NotImplementedException(),
        };

        return VerifyRequirement(normalizedValue);
    }

    /// <summary>
    /// Performs the actual comparison of a nullable double value against TargetValue.
    ///
    /// For most CompareTypes, null is treated as 0 (via the ?? 0 fallback).
    /// The exceptions are Exist, NotExist, and NotEqualNotExist which specifically
    /// test for the presence or absence of the property (null = "property doesn't exist").
    ///
    /// Bit-flag comparisons (HasBits / NotHasBits):
    ///   These use bitwise AND to check whether specific bits are set in the property value.
    ///   Example: HasBits with TargetValue = 0b1010 means both bit 1 and bit 3 must be set.
    ///
    /// Note on NotEqualNotExist: returns true if the property is null OR if its value
    /// doesn't equal TargetValue. (The original author noted uncertainty about this logic.)
    ///
    /// The commented-out block at the bottom is an earlier inverted version of this logic
    /// — kept for reference in case the inversion is needed again.
    /// </summary>
    public bool VerifyRequirement(double? prop)
    {
        return Type switch
        {
            CompareType.GreaterThan      => (prop ?? 0) > TargetValue,
            CompareType.GreaterThanEqual => (prop ?? 0) >= TargetValue,
            CompareType.LessThan         => (prop ?? 0) < TargetValue,
            CompareType.LessThanEqual    => (prop ?? 0) <= TargetValue,
            CompareType.NotEqual         => (prop ?? 0) != TargetValue,
            CompareType.Equal            => (prop ?? 0) == TargetValue,
            CompareType.NotEqualNotExist => prop == null || prop.Value != TargetValue, // true if missing OR not equal
            CompareType.NotExist         => prop is null,                               // true only if property is absent
            CompareType.Exist            => prop is not null,                           // true only if property is present
            CompareType.NotHasBits       => ((int)(prop ?? 0) & (int)TargetValue) == 0,             // none of the target bits are set
            CompareType.HasBits          => ((int)(prop ?? 0) & (int)TargetValue) == (int)TargetValue, // all target bits are set
            _ => true, // unknown compare type — default to "pass" so it doesn't silently block everything
        };
    }
}
