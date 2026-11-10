using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBoxDev.PanelLayout
{
    /// <summary>Uma seção no layout salvo — é o formato persistido (configuração do PX DEV e
    /// preferências portáteis).</summary>
    public sealed class PanelSectionLayoutEntry
    {
        public PanelSectionLayoutEntry()
        {
        }

        public PanelSectionLayoutEntry(string id, bool visible)
        {
            Id = id;
            Visible = visible;
        }

        public string Id { get; set; } = "";
        public bool Visible { get; set; } = true;
    }

    /// <summary>
    /// Ordem e visibilidade das seções do painel (só Ids — ver <see cref="PanelSectionCatalog"/>).
    /// Nunca lança com o que vier da configuração: vazios e repetidos são descartados (vale a
    /// primeira ocorrência); seções conhecidas que faltam no salvo (seção nova depois de uma
    /// atualização) entram VISÍVEIS no fim; Ids desconhecidos (seção removida, ou gravados por
    /// uma versão mais nova) não aparecem, mas são PRESERVADOS no fim ao salvar — mesmo
    /// princípio de <see cref="Favorites.PanelFavoritesList"/>.
    /// </summary>
    public sealed class PanelSectionLayout
    {
        private readonly List<PanelSectionLayoutEntry> _entries;

        // Seções criadas pelo usuário (PanelCustomization) — conhecidas como as do catálogo.
        private readonly List<string> _customIds;

        public PanelSectionLayout(IEnumerable<PanelSectionLayoutEntry?>? stored, IEnumerable<string>? customSectionIds = null)
        {
            _customIds = (customSectionIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
            _entries = new List<PanelSectionLayoutEntry>();
            foreach (var entry in stored ?? Enumerable.Empty<PanelSectionLayoutEntry?>())
            {
                var id = entry?.Id?.Trim();
                if (!string.IsNullOrEmpty(id) && !_entries.Any(e => string.Equals(e.Id, id, StringComparison.Ordinal)))
                {
                    _entries.Add(new PanelSectionLayoutEntry(id!, entry!.Visible));
                }
            }

            // Seção que o layout salvo não conhece (criada depois dele) entra visível logo
            // DEPOIS da seção que a antecede no catálogo — a Montagem aparece junto do Grid, não
            // no fim do painel. Sem antecessora no layout, entra no início.
            var catalog = PanelSectionCatalog.All;
            for (var i = 0; i < catalog.Count; i++)
            {
                if (_entries.Any(e => string.Equals(e.Id, catalog[i].Id, StringComparison.Ordinal))) continue;

                var insertAt = 0;
                for (var previous = i - 1; previous >= 0; previous--)
                {
                    var index = _entries.FindIndex(e => string.Equals(e.Id, catalog[previous].Id, StringComparison.Ordinal));
                    if (index >= 0)
                    {
                        insertAt = index + 1;
                        break;
                    }
                }

                _entries.Insert(insertAt, new PanelSectionLayoutEntry(catalog[i].Id, true));
            }

            // Seção criada que ainda não está no layout: no fim, visível.
            foreach (var id in _customIds.Where(id => !_entries.Any(e => string.Equals(e.Id, id, StringComparison.Ordinal))))
            {
                _entries.Add(new PanelSectionLayoutEntry(id, true));
            }
        }

        private bool IsKnown(string id) => PanelSectionCatalog.Find(id) != null || _customIds.Contains(id, StringComparer.Ordinal);

        /// <summary>Mesmo layout, reconhecendo outro conjunto de seções criadas (depois de criar ou
        /// remover uma).</summary>
        public PanelSectionLayout WithCustomSections(IEnumerable<string> customSectionIds) => new PanelSectionLayout(ToPersistAll(), customSectionIds);

        // Todas as entradas guardadas (inclusive de seções criadas que deixaram de existir).
        private List<PanelSectionLayoutEntry> ToPersistAll() => _entries.Select(Copy).ToList();

        /// <summary>Ordem do catálogo, todas visíveis.</summary>
        public static PanelSectionLayout Default() => new PanelSectionLayout(null);

        /// <summary>Seções conhecidas, na ordem do usuário — o que o painel e a janela mostram.</summary>
        public IReadOnlyList<PanelSectionLayoutEntry> Sections =>
            _entries.Where(e => IsKnown(e.Id)).Select(Copy).ToList();

        // Seção criada que foi removida não é "desconhecida de uma versão mais nova": não é
        // preservada.
        public IReadOnlyList<PanelSectionLayoutEntry> UnknownEntries =>
            _entries.Where(e => !IsKnown(e.Id) && !e.Id.StartsWith(PanelCustomization.CustomSectionPrefix, StringComparison.Ordinal)).Select(Copy).ToList();

        /// <summary>O que é gravado: conhecidas na ordem atual, desconhecidas preservadas no fim.</summary>
        public List<PanelSectionLayoutEntry> ToPersist() => Sections.Concat(UnknownEntries).ToList();

        public bool IsVisible(string id) =>
            _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal))?.Visible ?? true;

        public bool SetVisible(string id, bool visible)
        {
            var entry = _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (entry == null || !IsKnown(id) || entry.Visible == visible) return false;
            entry.Visible = visible;
            return true;
        }

        public bool MoveUp(string id) => Move(id, -1);

        public bool MoveDown(string id) => Move(id, +1);

        /// <summary>Volta à ordem do catálogo com tudo visível; desconhecidas continuam
        /// preservadas no fim. Não toca em mais nada (Favoritos, por exemplo, são outro campo).</summary>
        public void ResetToDefault()
        {
            var unknown = UnknownEntries;
            _entries.Clear();
            _entries.AddRange(PanelSectionCatalog.All.Select(s => new PanelSectionLayoutEntry(s.Id, true)));
            _entries.AddRange(_customIds.Select(id => new PanelSectionLayoutEntry(id, true)));
            _entries.AddRange(unknown);
        }

        public bool IsDefault =>
            Sections.Select(e => e.Id).SequenceEqual(PanelSectionCatalog.All.Select(s => s.Id).Concat(_customIds))
            && Sections.All(e => e.Visible);

        public PanelSectionLayout Clone() => new PanelSectionLayout(ToPersist(), _customIds);

        // Troca de lugar com a vizinha CONHECIDA (desconhecidas não contam como posição).
        private bool Move(string id, int direction)
        {
            var known = Sections.Select(e => e.Id).ToList();
            var index = known.IndexOf(id);
            var neighbor = index + direction;
            if (index < 0 || neighbor < 0 || neighbor >= known.Count) return false;

            var a = _entries.FindIndex(e => e.Id == id);
            var b = _entries.FindIndex(e => e.Id == known[neighbor]);
            (_entries[a], _entries[b]) = (_entries[b], _entries[a]);
            return true;
        }

        private static PanelSectionLayoutEntry Copy(PanelSectionLayoutEntry e) => new PanelSectionLayoutEntry(e.Id, e.Visible);
    }
}
