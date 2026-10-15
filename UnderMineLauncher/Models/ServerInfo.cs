using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnderMine.Models;

public record ServerList(
    string Id, 
    string ServerName,
    string GameVersion,
    string LoaderType,
    string IPAddress,
    ServerConfig Config
    );

public record ServerConfig(
    string LoaderVersion,
    string InstallerLink,
    string ModsLink,
    string ShadersLink,
    string ConfigLink,
    string KybejsLink);
