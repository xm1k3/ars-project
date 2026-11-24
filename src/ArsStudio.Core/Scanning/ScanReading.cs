using System.Collections.Generic;

namespace ArsStudio.Core.Scanning;

public sealed record ScanReading(CubeState State, IReadOnlySet<int> Doubtful);
