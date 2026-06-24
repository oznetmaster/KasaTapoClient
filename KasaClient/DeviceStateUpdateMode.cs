// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KasaTapoClient;

/// <summary>
/// Specifies how cached device state should be handled after executing a raw command.
/// </summary>
public enum DeviceStateUpdateMode
	{
	/// <summary>
	/// Do not refresh cached device state after the command completes.
	/// </summary>
	None,

	/// <summary>
	/// Refresh cached device state after the command completes.
	/// </summary>
	UpdateAfterCommand,
	}
