using System;
using System.Collections.Generic;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Services;
using Frontend_interfaz_Multimodal_HMI.ViewModels;
using NSubstitute;

namespace FrontEnd.InterfazHMI.Tests.Unit.ViewModels;

public class MainViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private MainViewModel CreateSut() => new(_navigation);

    [Fact]
    public void Constructor_NavigatesToWelcome()
    {
        _ = CreateSut();

        _navigation.Received(1).NavigateTo("welcome");
    }

    [Fact]
    public void Navigation_ReturnsInjectedService()
    {
        var sut = CreateSut();

        Assert.Same(_navigation, sut.Navigation);
    }

    [Fact]
    public void GoHomeCommand_CanExecute_ReturnsTrue()
    {
        var sut = CreateSut();

        Assert.True(sut.GoHomeCommand.CanExecute(null));
    }

    [Fact]
    public void GoHomeCommand_Execute_NavigatesToWelcome()
    {
        var sut = CreateSut();
        _navigation.ClearReceivedCalls(); // ignora la navegacion inicial del constructor

        sut.GoHomeCommand.Execute(null);

        _navigation.Received(1).NavigateTo("welcome");
    }
}
