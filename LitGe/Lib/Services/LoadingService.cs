using LitGe.Pages;
using Mopups.Interfaces;
using Mopups.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LitGe.Lib.Services
{
    internal class LoadingService : ILoadingService, IDisposable
    {
        private readonly IPopupNavigation _navigation;

        public LoadingService()
        {
            _navigation = MopupService.Instance;
        }

        public async void Dispose()
        {
            await _navigation.PopAsync();
        }

        public async Task<IDisposable> Show()
        {
            await _navigation.PushAsync(new Loading(), true);
            return this;
        }
    }

    public interface ILoadingService
    {
        Task<IDisposable> Show();
    }
}