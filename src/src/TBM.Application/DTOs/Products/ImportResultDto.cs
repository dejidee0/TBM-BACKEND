using System;
using System.Collections.Generic;

namespace TBM.Application.DTOs.Products;

public class ImportResultDto
{
    public bool Success { get; set; }
    public int TotalRows { get; set; }
    public int SuccessfullyImported { get; set; }
    public int Failed { get; set; }
    public List<ImportResultRowDto> Results { get; set; } = new();
}

public class ImportResultRowDto
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}
