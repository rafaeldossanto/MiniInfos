using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class BuiltInPetTests
{
    [Fact]
    public void TheOwlIsTheOnlyBuiltInAndTheDefaultForAnUnknownChoice()
    {
        Assert.Equal([PetAppearances.Athena], PetAppearances.BuiltIns.Select(pet => pet.Appearance));
        Assert.Same(PetAppearances.Athena, PetAppearances.Load(null));
        Assert.Same(PetAppearances.Athena, PetAppearances.Load("missing.pet"));
    }

    [Fact]
    public void APackagesIdIsNeverTakenForABuiltIn()
    {
        Assert.True(PetAppearances.IsBuiltIn(null));
        Assert.False(PetAppearances.IsBuiltIn("community.buster-beaver"));
        Assert.Null(PetAppearances.BuiltIn("community.buster-beaver"));
    }
}
