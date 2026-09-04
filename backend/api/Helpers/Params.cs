using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.domain.entities;

namespace backend.api.Helpers;
public class Params
{
    private int _pageSize = 5;
    private const int MaxPageSize = 50;
    private int _pageIndex = 1;
    private string _search= string.Empty;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);

    }
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = (value <= 0) ? 1 : value;
    }
    public string Search
    {
        get => _search;
        set => _search = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    }
    public Region? Region { get; set; }
}