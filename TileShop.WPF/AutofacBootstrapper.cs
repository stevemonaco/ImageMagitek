using Autofac;
using Stylet;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace TileShop.WPF;

/// <summary>
/// Bootstrapper base for Autofac IoC
/// </summary>
/// <remarks>Original source from Stylet's Bootstrapper project</remarks>
public class AutofacBootstrapper<TRootViewModel> : BootstrapperBase where TRootViewModel : class
{
    protected IContainer _container;

    private object _rootViewModel;
    protected virtual object RootViewModel
    {
        get { return _rootViewModel ??= GetInstance(typeof(TRootViewModel)); }
    }

    protected override void ConfigureBootstrapper()
    {
        var builder = new ContainerBuilder();
        DefaultConfigureIoC(builder);
        ConfigureIoC(builder);
        _container = builder.Build();
    }

    /// <summary>
    /// Carries out default configuration of the IoC container. Override if you don't want to do this
    /// </summary>
    protected virtual void DefaultConfigureIoC(ContainerBuilder builder)
    {
        var viewManagerConfig = ConfigureViewManagerConfig();
        builder.RegisterInstance<IViewManager>(new ViewManager(viewManagerConfig));

        builder.RegisterInstance<IWindowManagerConfig>(this).ExternallyOwned();
        builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
        builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();
        builder.RegisterType<MessageBoxViewModel>().As<IMessageBoxViewModel>().ExternallyOwned(); // Not singleton!

        ConfigureViewModels(builder);
        ConfigureViews(builder);
    }

    protected virtual void ConfigureViewModels(ContainerBuilder builder)
    {
        var vmTypes = GetType()
            .Assembly
            .GetTypes()
            .Where(x => x.Name.EndsWith("ViewModel"))
            .Where(x => !x.IsAbstract && !x.IsInterface);

        foreach (var vmType in vmTypes)
            builder.RegisterType(vmType);
    }

    protected virtual void ConfigureViews(ContainerBuilder builder)
    {
        var viewTypes = GetType().Assembly.GetTypes().Where(x => x.Name.EndsWith("View"));

        foreach (var viewType in viewTypes)
            builder.RegisterType(viewType);
    }

    protected virtual ViewManagerConfig ConfigureViewManagerConfig()
    {
        return new ViewManagerConfig()
        {
            ViewFactory = GetInstance,
            ViewAssemblies = new List<Assembly>() { GetType().Assembly }
        };
    }

    /// <summary>
    /// Override to add your own types to the IoC container.
    /// </summary>
    protected virtual void ConfigureIoC(ContainerBuilder builder) { }

    public override object GetInstance(Type type)
    {
        return _container.Resolve(type);
    }

    protected override void Launch()
    {
        base.DisplayRootView(RootViewModel);
    }

    public override void Dispose()
    {
        ScreenExtensions.TryDispose(_rootViewModel);
        if (_container != null)
            _container.Dispose();

        base.Dispose();
    }
}
