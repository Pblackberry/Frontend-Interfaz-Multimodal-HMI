using System;
using System.Collections.Generic;
using System.Text;

namespace FrontEnd.InterfazHMI.Tests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MessengerCollection
{
    public const string Name = "Messenger";
}
