using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public class AlertLogSummary
{
	public long Id { get; set; }

	public int AlertId { get; set; }

	public int ActionId { get; set; }

	public int? ActionUserId { get; set; }

	public string? ActionUserName { get; set; }

	public int? EntityId { get; set; }

	public string? Data { get; set; }

	public DateTime Timestamp { get; set; }
}
