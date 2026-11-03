using FrontEnd.InterfazHMI.Tests.Support;
using Frontend_interfaz_Multimodal_HMI.Services;

namespace FrontEnd.InterfazHMI.Tests.Unit.Services;

public class OrderSessionTests
{
    private readonly OrderSession _sut = new();

    [Fact]
    public void NewSession_HasNoSelectedDrink()
    {
        Assert.Null(_sut.SelectedDrink);
    }

    [Fact]
    public void Select_StoresTheSameInstance()
    {
        var item = TestData.Item("Mojito");

        _sut.Select(item);

        Assert.Same(item, _sut.SelectedDrink);
    }

    [Fact]
    public void Select_Twice_KeepsTheLastDrink()
    {
        var daiquiri = TestData.Item("Daiquiri");
        _sut.Select(TestData.Item("Mojito"));

        _sut.Select(daiquiri);

        Assert.Same(daiquiri, _sut.SelectedDrink);
    }

    [Fact]
    public void Clear_RemovesTheSelectedDrink()
    {
        _sut.Select(TestData.Item("Mojito"));

        _sut.Clear();

        Assert.Null(_sut.SelectedDrink);
    }

    [Fact]
    public void Clear_WithoutSelection_DoesNotThrow()
    {
        var exception = Record.Exception(() => _sut.Clear());

        Assert.Null(exception);
        Assert.Null(_sut.SelectedDrink);
    }
}
