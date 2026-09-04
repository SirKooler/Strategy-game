using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player data used when a match starts.
/// Only username and deck for now. Deck entries are character definitions.
/// </summary>
[CreateAssetMenu(menuName = "Strategy/Player", fileName = "Player")]
public class Player : ScriptableObject
{
    public string username;

    /// <summary>Characters this player brings into a match.</summary>
    public List<UnitStats> deck = new List<UnitStats>();
}
