﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Utility.HttpAction.Action;
using Utility.HttpAction.Event;
using Utility.HttpAction.Service;
using WebWeChat.Im.Action;
using WebWeChat.Im.Core;
using WebWeChat.Im.Event;
using WebWeChat.Im.Module;
using WebWeChat.Im.Module.Impl;
using WebWeChat.Im.Module.Interface;
using WebWeChat.Im.Service.Impl;
using WebWeChat.Im.Service.Interface;

namespace WebWeChat.Im
{
    public class WebWeChatClient : IWebWeChatClient
    {
        private readonly WeChatNotifyEventListener _notifyListener;
        private readonly ServiceCollection _services;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public WebWeChatClient(WeChatNotifyEventListener notifyListener = null)
        {
            _services = new ServiceCollection();
            Startup.ConfigureServices(_services);

            _services.AddSingleton<IWeChatContext>(this);

            // 模块
            _services.AddSingleton<ILoginModule, LoginModule>();
            _services.AddSingleton<StoreModule>();
            _services.AddSingleton<SessionModule>();
            _services.AddSingleton<AccountModule>();

            // 服务
            _services.AddSingleton<IHttpService, WeChatHttp>();
            _services.AddSingleton<ILogger>(provider => new WeChatLogger(this, LogLevel.Information));
            _services.AddSingleton<IWeChatActionFactory, WeChatActionFactory>();
            
            _serviceProvider = _services.BuildServiceProvider();
            Startup.Configure(_serviceProvider);

            _notifyListener = notifyListener;
            _logger = GetSerivce<ILogger>();
        }

        public Task<ActionEvent> Login(ActionEventListener listener = null)
        {
            var login = GetModule<ILoginModule>();
            return login.Login(listener);
        }

        /// <inheritdoc />
        public void FireNotify(WeChatNotifyEvent notifyEvent)
        {
            try
            {
                _notifyListener?.Invoke(this, notifyEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"FireNotify Error!! {ex}", ex);
            }
        }

        public Task FireNotifyAsync(WeChatNotifyEvent notifyEvent)
        {
            return Task.Run(()=> FireNotify(notifyEvent));
        }

        /// <inheritdoc />
        public T GetSerivce<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <inheritdoc />
        public T GetModule<T>() where T : IWeChatModule
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// 销毁所有模块和服务
        /// </summary>
        public void Dispose()
        {
            try
            {
                foreach (var service in _services)
                {
                    var serviceType = service.ServiceType;
                    if (typeof(IDisposable).IsAssignableFrom(serviceType) &&
                        (typeof(IWeChatModule).IsAssignableFrom(serviceType)
                        || typeof(IWeChatService).IsAssignableFrom(serviceType)))
                    {
                        var obj = (IDisposable)service.ImplementationInstance;
                        obj.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"销毁所有模块和服务失败: {e}");
            }
        }
    }
}
