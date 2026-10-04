using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Customlist
{
    public uint ClListSlno { get; set; }

    /// <summary>
    /// ID OF THE lIST FOR NEW TAKE SELECT MAX(cl_list_id)+1
    /// </summary>
    public uint ClListId { get; set; }

    /// <summary>
    /// ID OF THE lIST FOR NEW TAKE SELECT MAX(cl_list_item_id)+1
    /// </summary>
    public uint? ClListItemId { get; set; }

    /// <summary>
    /// 0=&gt;List Name 1=&gt;list items 2=&gt;Context 3=&gt;Template 4=&gt;Sentence 5=&gt; SavedTemplate 6=&gt;CustomButton
    /// </summary>
    public uint ClListType { get; set; }

    public string? ClListItemShort { get; set; }

    public string? ClListItemLong { get; set; }

    /// <summary>
    /// Flow level for List Designation
    /// </summary>
    public int? ClListItemLevel { get; set; }

    public int? ClOrder { get; set; }

    public bool? ClDeleted { get; set; }

    public int? ClCreator { get; set; }
}
