using System;
using System.Collections.Generic;
using System.Windows;
using Caliburn.Micro;
using Gammtek.Conduit.MassEffect.DifficultyEditor.ViewModels;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor
{
	public class AppBootstrapper : BootstrapperBase
	{
		private SimpleContainer _container;

		public AppBootstrapper()
		{
			Initialize();
		}

		protected override void BuildUp(object instance)
		{
			_container.BuildUp(instance);
		}

		protected override void Configure()
		{
			_container = new SimpleContainer();

			_container.Singleton<IWindowManager, WindowManager>();
			_container.Singleton<IEventAggregator, EventAggregator>();
			_container.PerRequest<IShell, ShellViewModel>();
		}

		protected override IEnumerable<object> GetAllInstances(Type service)
		{
			return _container.GetAllInstances(service);
		}

		protected override object GetInstance(Type service, string key)
		{
			var instance = _container.GetInstance(service, key);

			if (instance != null)
			{
				return instance;
			}

			throw new InvalidOperationException("Could not locate any instances.");
		}

		protected override void OnStartup(object sender, StartupEventArgs e)
		{
			DisplayRootViewFor<IShell>();
		}
	}
}
