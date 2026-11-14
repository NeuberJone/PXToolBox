using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PXToolBox.Core.Nesting
{
    /// <summary>Em que ordem as peças entram no encaixe (todas da maior para a menor, pelo critério).
    /// Empate: maior área da caixa, depois a ordem de entrada.</summary>
    public enum NestOrder
    {
        LongestSide,
        Area,
        Width,
        Height,
        Perimeter,
    }

    public static class NestOrders
    {
        /// <summary>As ordens que a busca tenta, nesta sequência.</summary>
        public static IReadOnlyList<NestOrder> All { get; } =
            new[] { NestOrder.LongestSide, NestOrder.Area, NestOrder.Width, NestOrder.Height, NestOrder.Perimeter };

        public static string Name(NestOrder order) => order switch
        {
            NestOrder.Area => "maior área primeiro",
            NestOrder.Width => "mais largas primeiro",
            NestOrder.Height => "mais altas primeiro",
            NestOrder.Perimeter => "maior perímetro primeiro",
            _ => "maior lado primeiro",
        };

        internal static IEnumerable<T> Sort<T>(IEnumerable<T> pieces, Func<T, double> width, Func<T, double> height, NestOrder order) =>
            pieces
                .Select((piece, position) => (piece, position, w: width(piece), h: height(piece)))
                .OrderByDescending(p => Key(order, p.w, p.h))
                .ThenByDescending(p => p.w * p.h)
                .ThenBy(p => p.position)
                .Select(p => p.piece);

        private static double Key(NestOrder order, double w, double h) => order switch
        {
            NestOrder.Area => w * h,
            NestOrder.Width => w,
            NestOrder.Height => h,
            NestOrder.Perimeter => w + h,
            _ => Math.Max(w, h),
        };
    }

    /// <summary>Nota de um resultado para comparar tentativas: mais peças encaixadas, depois mais área de
    /// peças, depois menos áreas usadas, depois menos comprimento usado (na última área).</summary>
    public readonly struct NestScore
    {
        private const double Tolerance = 0.01;

        public NestScore(int placed, double placedArea, int areasUsed, double usedLength)
        {
            Placed = placed;
            PlacedArea = placedArea;
            AreasUsed = areasUsed;
            UsedLength = usedLength;
        }

        public int Placed { get; }
        public double PlacedArea { get; }
        public int AreasUsed { get; }
        public double UsedLength { get; }

        public bool IsBetterThan(NestScore other)
        {
            if (Placed != other.Placed) return Placed > other.Placed;
            if (Math.Abs(PlacedArea - other.PlacedArea) > Tolerance) return PlacedArea > other.PlacedArea;
            if (AreasUsed != other.AreasUsed) return AreasUsed < other.AreasUsed;
            return UsedLength < other.UsedLength - Tolerance;
        }

        public static NestScore Of(NestResult result) =>
            new NestScore(result.Placements.Count, result.Placements.Sum(p => p.Width * p.Height), 1, result.UsedLength);

        public static NestScore Of(ContourNestResult result) =>
            new NestScore(result.Placements.Count, result.Placements.Sum(p => p.Piece.Width * p.Piece.Height), 1, result.UsedLength);

        public static NestScore Of(AreaNestResult<NestResult, NestItem> result) =>
            new NestScore(result.Fills.Sum(f => f.Result.Placements.Count),
                result.Fills.Sum(f => f.Result.Placements.Sum(p => p.Width * p.Height)),
                result.Fills.Count, result.Fills.Count == 0 ? 0 : result.Fills[result.Fills.Count - 1].Result.UsedLength);

        public static NestScore Of(AreaNestResult<ContourNestResult, ContourPiece> result) =>
            new NestScore(result.Fills.Sum(f => f.Result.Placements.Count),
                result.Fills.Sum(f => f.Result.Placements.Sum(p => p.Piece.Width * p.Piece.Height)),
                result.Fills.Count, result.Fills.Count == 0 ? 0 : result.Fills[result.Fills.Count - 1].Result.UsedLength);
    }

    public sealed class NestSearchResult<TResult>
    {
        public NestSearchResult(TResult result, NestOrder order, int tried, int total, bool timedOut, bool cancelled, double seconds)
        {
            Result = result;
            Order = order;
            Tried = tried;
            Total = total;
            TimedOut = timedOut;
            Cancelled = cancelled;
            Seconds = seconds;
        }

        public TResult Result { get; }

        /// <summary>A ordem que deu o melhor resultado.</summary>
        public NestOrder Order { get; }

        /// <summary>Tentativas que terminaram (a interrompida não conta).</summary>
        public int Tried { get; }

        public int Total { get; }

        /// <summary>Parou antes de tentar tudo porque passou do tempo máximo.</summary>
        public bool TimedOut { get; }

        /// <summary>Parou antes de tentar tudo porque o usuário cancelou (depois da 1ª tentativa).</summary>
        public bool Cancelled { get; }

        public double Seconds { get; }
    }

    /// <summary>
    /// Busca do melhor encaixe (N4 de docs/reference-feature-roadmap.md): roda o mesmo encaixe em cada ordem
    /// de <see cref="NestOrders.All"/> e fica com a de melhor <see cref="NestScore"/> (empate: a primeira).
    /// <list type="bullet">
    /// <item>A 1ª tentativa sempre termina (só o cancelamento do usuário a interrompe, com
    /// OperationCanceledException): sempre há resultado.</item>
    /// <item>Tempo máximo: a partir da 2ª, a tentativa que passar do limite é interrompida e descartada; o
    /// melhor até ali fica. Cancelar depois da 1ª também devolve o melhor até ali.</item>
    /// <item>Mesmas peças e opções → mesmo resultado, desde que todas as ordens caibam no tempo.</item>
    /// </list>
    /// </summary>
    public static class NestSearch
    {
        public static NestSearchResult<TResult> Best<TResult>(IReadOnlyList<NestOrder> orders,
            Func<NestOrder, CancellationToken, TResult> run, Func<TResult, NestScore> score, TimeSpan limit,
            CancellationToken cancel = default, Action<int, int>? progress = null)
        {
            if (orders.Count == 0) throw new ArgumentException("Nenhuma ordem para tentar.");
            var clock = Stopwatch.StartNew();
            progress?.Invoke(1, orders.Count);
            var best = run(orders[0], cancel);
            var bestScore = score(best);
            var bestOrder = orders[0];
            var tried = 1;
            var timedOut = false;
            var cancelled = false;
            for (var k = 1; k < orders.Count; k++)
            {
                if (cancel.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var remaining = limit - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    timedOut = true;
                    break;
                }

                progress?.Invoke(k + 1, orders.Count);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                deadline.CancelAfter(remaining);
                TResult candidate;
                try
                {
                    candidate = run(orders[k], deadline.Token);
                }
                catch (OperationCanceledException)
                {
                    if (cancel.IsCancellationRequested) cancelled = true;
                    else timedOut = true;
                    break;
                }

                tried++;
                var candidateScore = score(candidate);
                if (candidateScore.IsBetterThan(bestScore))
                {
                    best = candidate;
                    bestScore = candidateScore;
                    bestOrder = orders[k];
                }
            }

            return new NestSearchResult<TResult>(best, bestOrder, tried, orders.Count, timedOut, cancelled, clock.Elapsed.TotalSeconds);
        }
    }
}
