// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

using System.Threading.Tasks;

namespace Common
{

    public interface IUpdateableDevice
    {
        string Firmware {get;}
        bool CanUpdate();
        Task UpdateFirmware(string newFirmware);
    }
}
