﻿using System.Globalization;
using System.Windows;

namespace CustomLocalizer
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1
    {
        public Window1()
        {
            // Set Custom Localizer
            Ptv.XServer.Controls.Map.Localization.MapLocalizer.Active = new CustomMapLocalizer();

            InitializeComponent();
        }
    }
}
