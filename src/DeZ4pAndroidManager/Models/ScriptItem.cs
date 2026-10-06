// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System.Collections.ObjectModel;
namespace DeZ4pAndroidManager.Models;

public class ScriptItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public ObservableCollection<string> Commands { get; } = new();
    public string Icon { get; set; } = "\uE943";
    public string Color { get; set; } = "#4F8CFF";
}