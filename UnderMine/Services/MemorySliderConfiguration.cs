using System;
using System.Globalization;
using System.Threading.Tasks;
using UnderMine.Models;
namespace UnderMine.Services;

public class MemorySliderConfiguration
{
    private readonly SystemInfo _systemInfo;

    public MemorySliderConfiguration(SystemInfo systemInfo)
    {
        _systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
    }

    public async Task<SliderValues> InitializeMemorySliderAsync()
    {
        var totalMemory = _systemInfo.TotalMemory;
        return await Task.FromResult(CalculatingValues(totalMemory));
    }
    
    private SliderValues CalculatingValues(double totalMemory)
    {
        var min = 4096;
        var max = totalMemory;
        
        if (max >= 16384)
        {
            max = 16384;
        }
        
        var steps = 5;
        var step = Math.Round((max - min) / (steps - 1), 0);

        return new SliderValues
        {
            Step = step,
            FirstValue = min.ToString(CultureInfo.InvariantCulture),
            SecondValue = (min + step * 1).ToString(CultureInfo.InvariantCulture),
            ThirdValue = (min + step * 2).ToString(CultureInfo.InvariantCulture),
            FourthValue = (min + step * 3).ToString(CultureInfo.InvariantCulture),
            FifthValue = (min + step * 4).ToString(CultureInfo.InvariantCulture),
        };
    }
}

public class SliderValues 
{
    public double Step { get; set; }
    public string FirstValue {get; init;} = null!;
    public string SecondValue {get; init;} = null!;
    public string ThirdValue {get; init;} = null!;
    public string FourthValue {get; init;} = null!;
    public string FifthValue {get; init;} = null!;
}