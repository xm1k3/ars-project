using System;

namespace ArsStudio.Core;

public sealed class InvalidCubeException(string message) : Exception(message);
