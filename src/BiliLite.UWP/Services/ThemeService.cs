﻿using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml;
using BiliLite.Extensions;
using BiliLite.Models.Common;
using Windows.UI.Xaml.Controls;

namespace BiliLite.Services
{
    public class ThemeService
    {
        private ResourceDictionary m_defaultColorsResource;
        private ElementTheme m_theme;

        public ThemeService()
        {
            m_theme = (ElementTheme)SettingService.GetValue<int>(SettingConstants.UI.THEME, 0);
            if (m_theme == ElementTheme.Default)
            {
                m_theme = (ElementTheme)(App.Current.RequestedTheme - 1);
            }
        }

        public async Task Init()
        {
            var colorFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Themes/Colors.xaml"));
            var colorsText = await FileIO.ReadTextAsync(colorFile);
            m_defaultColorsResource = (ResourceDictionary)XamlReader.Load(colorsText);
        }

        public ResourceDictionary ThemeResource
        {
            get
            {
                if(m_theme==ElementTheme.Light)return m_defaultColorsResource.ThemeDictionaries["Light"] as ResourceDictionary;
                return m_defaultColorsResource.ThemeDictionaries["Dark"] as ResourceDictionary;
            }
        }

        public void InitTitleBar()
        {
            AppExtensions.HandleTitleTheme();
        }

        public void SetTheme(ElementTheme theme)
        {
            m_theme = theme;
            SettingService.SetValue(SettingConstants.UI.THEME, (int)theme);
            var rootFrame = Window.Current.Content as Frame;
            switch (theme)
            {
                case ElementTheme.Light:
                    rootFrame.RequestedTheme = ElementTheme.Light;
                    break;
                case ElementTheme.Dark:
                    rootFrame.RequestedTheme = ElementTheme.Dark;
                    break;
                //case 3:
                //    // TODO: 切换自定义主题
                //    rootFrame.Resources = Application.Current.Resources.ThemeDictionaries["Pink"] as ResourceDictionary;
                //    break;
                default:
                    rootFrame.RequestedTheme = ElementTheme.Default;
                    break;
            }
            InitTitleBar();
        }
    }
}