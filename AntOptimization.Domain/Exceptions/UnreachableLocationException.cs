namespace AntOptimization.Domain.Exceptions;

/// <summary>Raised when the routing provider cannot compute a distance between two locations.</summary>
public class UnreachableLocationException(int fromIndex, int toIndex)
    : Exception($"Não há rota entre os pontos {fromIndex + 1} e {toIndex + 1} (ponto inalcançável pela malha viária).")
{
    public int FromIndex { get; } = fromIndex;
    public int ToIndex { get; } = toIndex;
}
