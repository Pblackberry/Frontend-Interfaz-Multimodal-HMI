using System;
using System.Collections.Generic;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using NSubstitute;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class WelcomeViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private WelcomeViewModel CreateSut() => new(_navigation);

    [Fact]
    public void Constructor_DoesNotNavigate()
    {
        _ = CreateSut();

        Assert.Empty(_navigation.ReceivedCalls());
    }

    [Fact]
    public void StartOrderCommand_CanExecute_ReturnsTrue()
    {
        var sut = CreateSut();

        Assert.True(sut.StartOrderCommand.CanExecute(null));
    }

    [Fact]
    public void StartOrderCommand_Execute_NavigatesToMenu()
    {
        var sut = CreateSut();

        sut.StartOrderCommand.Execute(null);

        _navigation.Received(1).NavigateTo("menu");
    }

    [Fact]
    public void StartListeningCommand_Execute_DoesNotThrowNorNavigate()
    {
        var sut = CreateSut();

        var exception = Record.Exception(() => sut.StartListeningCommand.Execute(null));

        Assert.Null(exception);
        Assert.Empty(_navigation.ReceivedCalls());
    }
}

