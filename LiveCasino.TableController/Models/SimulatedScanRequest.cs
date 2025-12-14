using System.ComponentModel.DataAnnotations;

namespace LiveCasino.TableController.Models;

public class SimulatedScanRequest
{
    [Required]
    public string Payload { get; set; } = string.Empty;

    public string Source { get; set; } = "simulated";
}
