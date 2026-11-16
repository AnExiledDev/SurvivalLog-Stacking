namespace Stacking.Core;

/// <summary>
/// Which item and recipe tables a full write went to, so a write spread over frames can tell when it started on tables
/// the game no longer reads.
/// </summary>
public readonly record struct TableStamp(long Items, int ItemCount, long Recipes, int RecipeCount);
