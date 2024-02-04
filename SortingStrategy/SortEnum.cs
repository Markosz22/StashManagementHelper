﻿using System.ComponentModel;

namespace StashManagementHelper;

public enum SortEnum
{
    [Description("Default EFT sorting")]
    Default,

    [Description("Custom sorting strategy")]
    Custom
}