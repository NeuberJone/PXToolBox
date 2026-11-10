using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBoxDev.Favorites
{
    /// <summary>
    /// Lista ordenada de favoritos do painel (só os Ids — ver <see cref="PanelToolCatalog"/>).
    /// Nunca lança com o que vier da configuração: vazios e repetidos são descartados; Ids
    /// desconhecidos (ferramenta removida, ou gravados por uma versão mais nova) não aparecem,
    /// mas são PRESERVADOS no fim da lista ao salvar — abrir o painel numa versão mais antiga
    /// não apaga favoritos da mais nova.
    /// </summary>
    public sealed class PanelFavoritesList
    {
        private readonly List<string> _ids;

        public PanelFavoritesList(IEnumerable<string?>? storedIds)
        {
            _ids = new List<string>();
            foreach (var id in storedIds ?? Enumerable.Empty<string?>())
            {
                var trimmed = id?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !_ids.Contains(trimmed!, StringComparer.Ordinal))
                {
                    _ids.Add(trimmed!);
                }
            }
        }

        /// <summary>Ferramentas conhecidas, na ordem do usuário — o que o painel mostra.</summary>
        public IReadOnlyList<PanelToolCatalog.Tool> Visible =>
            _ids.Select(PanelToolCatalog.Find).Where(t => t != null).Select(t => t!).ToList();

        public IReadOnlyList<string> UnknownIds => _ids.Where(id => PanelToolCatalog.Find(id) == null).ToList();

        /// <summary>O que é gravado: visíveis na ordem atual, desconhecidos preservados no fim.</summary>
        public IReadOnlyList<string> IdsToPersist => Visible.Select(t => t.Id).Concat(UnknownIds).ToList();

        public bool Contains(string id) => PanelToolCatalog.Find(id) != null && _ids.Contains(id, StringComparer.Ordinal);

        public bool Add(string id)
        {
            if (PanelToolCatalog.Find(id) == null || Contains(id)) return false;
            _ids.Add(id);
            return true;
        }

        public bool Remove(string id) => _ids.Remove(id);

        public bool MoveUp(string id) => Move(id, -1);

        public bool MoveDown(string id) => Move(id, +1);

        /// <summary>Arrastar e soltar: leva <paramref name="id"/> para antes do favorito visível
        /// que está na posição <paramref name="insertBefore"/> (0 = início, Count = fim), contada
        /// na ordem ANTES de mover. Falso se nada muda (soltou no próprio lugar). Desconhecidos
        /// continuam preservados no fim.</summary>
        public bool MoveTo(string id, int insertBefore)
        {
            var visible = Visible.Select(t => t.Id).ToList();
            var from = visible.IndexOf(id);
            if (from < 0 || insertBefore < 0 || insertBefore > visible.Count) return false;
            if (insertBefore == from || insertBefore == from + 1) return false;

            var unknown = UnknownIds;
            visible.RemoveAt(from);
            visible.Insert(insertBefore > from ? insertBefore - 1 : insertBefore, id);
            _ids.Clear();
            _ids.AddRange(visible);
            _ids.AddRange(unknown);
            return true;
        }

        // Troca de lugar com o vizinho VISÍVEL (desconhecidos não contam como posição).
        private bool Move(string id, int direction)
        {
            var visible = Visible.Select(t => t.Id).ToList();
            var index = visible.IndexOf(id);
            var neighbor = index + direction;
            if (index < 0 || neighbor < 0 || neighbor >= visible.Count) return false;

            var a = _ids.IndexOf(id);
            var b = _ids.IndexOf(visible[neighbor]);
            (_ids[a], _ids[b]) = (_ids[b], _ids[a]);
            return true;
        }
    }
}
