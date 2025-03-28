# StashManagementHelper

A Single Player Tarkov (SPT) plugin that enhances stash management with advanced sorting capabilities.

## Features

### Customizable Stash Sorting
- Create your own sorting strategy or use enhanced EFT default sorting
- Automatically fold items to optimize space
- Merge identical item stacks
- Smart item rotation for optimal fit
- Configurable empty rows at the top for item dumping
- Bottom-up sorting option
  
![{1D31F86E-3274-4D1D-B9E6-FB12E472A80E}](https://github.com/user-attachments/assets/f2339a85-65ef-4e22-9bdc-6d01b43a2270)

### Sorting Strategies
Configure sorting based on multiple criteria:
- Container size
- Item size
- Item type (with customizable type ordering - Not working too great, work in progress)

![image](https://github.com/Markosz22/StashManagementHelper/assets/41615461/521ecce7-ceda-45cc-8ab2-7539cb11b550)

## Usage

1. Configure settings according to your preferences (default settings optimized for maximum space efficiency)
2. Press the sort button
3. Global options (1) apply to both default and custom sorting methods

### Custom Configuration
After first use, a `customSortConfig.json` file will be generated alongside the DLL. This file allows you to:
- Modify `sortOrder` to prioritize different sorting criteria
- Customize `itemTypeOrder` to define the sequence of item types when using type-based sorting
