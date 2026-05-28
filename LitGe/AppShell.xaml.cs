using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Behaviors;
using LitGe.Lib.Services;
using LitGe.Pages;
using Newtonsoft.Json;

namespace LitGe
{
    public partial class AppShell : Shell
    {
        [Obsolete]
        public AppShell()
        {
            InitializeComponent();

            BindingContext = this;

            Routing.RegisterRoute(nameof(Authenticator), typeof(Authenticator));
            Routing.RegisterRoute(nameof(NoInternetRealm), typeof(NoInternetRealm));

            Behaviors.Add(
                new StatusBarBehavior
                {
                    StatusBarStyle = CommunityToolkit.Maui.Core.StatusBarStyle.LightContent,
                    StatusBarColor = Colors.Black,
                }
            );
        }
    }
}
