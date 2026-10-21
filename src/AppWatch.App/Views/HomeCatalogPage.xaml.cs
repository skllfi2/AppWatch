using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AppWatch.ViewModels;

namespace AppWatch.Views;

/// <summary>
/// Combined Home + Catalog page with TabView.
/// Tab 0 = Home (tracked apps with groups), Tab 1 = Catalog (search/install).
/// </summary>
public sealed partial class HomeCatalogPage : Page
{
    public HomeCatalogPage()
    {
        InitializeComponent();
    }


}