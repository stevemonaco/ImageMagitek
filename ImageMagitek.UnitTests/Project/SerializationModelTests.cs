using System;
using System.Linq;
using System.Reflection;
using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class SerializationModelTests
{
    public static TheoryData<Type> SampleTypes => new(ModelSamples.Types);

    [Fact]
    public void ForeignColorSourceModel_SameValue_Equal()
    {
        var a = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));
        var b = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));

        Assert.Equal(a, b);
    }

    [Fact]
    public void ForeignColorSourceModel_DifferentModel_NotEqual()
    {
        var bgr15 = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));
        var abgr16 = new ProjectForeignColorSourceModel(new ColorAbgr16(0x7C1F));

        Assert.NotEqual(bgr15, abgr16);
    }

    [Fact]
    public void NativeColorSourceModel_SameColor_Equal()
    {
        var a = new ProjectNativeColorSourceModel(new ColorRgba32(0x11, 0x22, 0x33, 0x44));
        var b = new ProjectNativeColorSourceModel(new ColorRgba32(new ColorRgba32(0x11, 0x22, 0x33, 0x44).Color));
        var c = new ProjectNativeColorSourceModel(new ColorRgba32(0x11, 0x22, 0x33, 0x45));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Theory]
    [MemberData(nameof(SampleTypes))]
    public void EveryProperty_ChangesEquality(Type type)
    {
        var sample = ModelSamples.Create(type);
        var clone = type.GetMethod("<Clone>$")!;
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.SetMethod is { IsPublic: true });

        Assert.NotEmpty(properties);
        foreach (var property in properties)
        {
            var copy = clone.Invoke(sample, null)!;
            property.SetValue(copy, ModelSamples.Vary(property.GetValue(sample), property.PropertyType));

            Assert.False(sample.Equals(copy), $"Changing {type.Name}.{property.Name} did not change equality");
        }
    }

    [Theory]
    [MemberData(nameof(SampleTypes))]
    public void IndependentlyBuiltModels_EqualWithSameHash(Type type)
    {
        var a = ModelSamples.Create(type);
        var b = ModelSamples.Create(type, reversed: true);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
