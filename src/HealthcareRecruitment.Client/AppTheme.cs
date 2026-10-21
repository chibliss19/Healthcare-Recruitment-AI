using MudBlazor;

namespace HealthcareRecruitment.Client;

/// <summary>Black/pink dark theme applied app-wide via MudThemeProvider in MainLayout.</summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#FF2E93",
            Secondary = "#FF7AC6",
            Tertiary = "#FF2E93",
            Background = "#0A0A0A",
            Surface = "#161616",
            AppbarBackground = "#000000",
            DrawerBackground = "#0A0A0A",
            DrawerText = "#F5F5F5",
            TextPrimary = "#F5F5F5",
            TextSecondary = "#C9A6BB",
            ActionDefault = "#FF7AC6",
            LinesDefault = "#2A2A2A",
            TableLines = "#2A2A2A",
            Divider = "#2A2A2A",
            Success = "#3DDC97",
            Info = "#4FC3F7",
            Warning = "#FFB74D",
            Error = "#FF5470",
        },
    };
}
