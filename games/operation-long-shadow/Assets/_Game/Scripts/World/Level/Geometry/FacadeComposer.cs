using System.Collections.Generic;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Composes the openings of a facade from a building's facade programme (TECH_PLAN §7.15: "kit pieces with seeded
    /// variation"): bays of 2.6–3.4 m, doors and grilled windows at street level, roller shutters for shops, round-arched
    /// arcades, shuttered windows and balcony doors above, small windows and vents at the back. Realistic proportions:
    /// sills 0.9 m above the floor, windows 0.9–1.15 m wide and 1.3–1.6 m tall, doors 1.0–1.2 x 2.1–2.3 m.
    /// Deterministic per building seed and side.
    /// </summary>
    public static class FacadeComposer
    {
        /// <summary>Bays of a facade: count and width.</summary>
        public static int Bays(float length, DetRandom rng, out float bayWidth)
        {
            var target = rng.Range(2.6f, 3.4f);
            var n = Mathf.Max(1, Mathf.RoundToInt(length / target));
            bayWidth = length / n;
            return n;
        }

        /// <summary>Composes every storey band of one side.</summary>
        public static List<FacadeBand> Compose(BuildingSpec spec, int side, float length)
        {
            var bands = new List<FacadeBand>();
            var kind = spec.Facades[side];
            var rng = new DetRandom(DetRandom.Mix((ulong)spec.Seed * 2654435761UL + 1UL, side));
            var n = Bays(length, rng, out var bay);
            var arched = spec.Style == BuildingStyle.Colonial || spec.Style == BuildingStyle.Sandstone;
            var depth = spec.Style == BuildingStyle.Vernacular ? 0.42f : 0.34f;
            var doorBay = n >= 3 ? rng.Range(1, n - 1) : rng.Range(0, n);
            var winW = Mathf.Min(bay - 0.9f, rng.Range(0.9f, 1.15f));
            var floors = spec.Storeys.Length;
            var balconyStorey = spec.Balconies && floors > 1 ? 1 : -1;
            var balconyBays = new bool[n];
            if (balconyStorey > 0 && (kind == FacadeKind.Residential || kind == FacadeKind.Shops || kind == FacadeKind.Arcade))
            {
                for (var i = 0; i < n; i++) balconyBays[i] = rng.Chance(0.45f);
                if (n > 0 && !System.Array.Exists(balconyBays, b => b)) balconyBays[rng.Range(0, n)] = true;
            }

            for (var s = 0; s < floors; s++)
            {
                var band = new FacadeBand { Storey = s, V0 = spec.FloorAt(s), V1 = spec.FloorAt(s) + spec.Storeys[s] };
                bands.Add(band);
                var h = spec.Storeys[s];
                if (kind == FacadeKind.Party || kind == FacadeKind.Blank)
                {
                    if (kind == FacadeKind.Blank && rng.Chance(0.35f) && length > 3f)
                    {
                        var u = rng.Range(1f, length - 1f);
                        band.Openings.Add(WallOpening.Make(u - 0.25f, u + 0.25f, band.V0 + h - 0.9f, band.V0 + h - 0.45f, OpeningShape.Rect, depth, true, OpeningFill.Vent, rng.Range(0, 9999)));
                    }
                    continue;
                }
                if (s == 0)
                {
                    if (spec.NoGroundOpenings[side]) continue;
                    switch (kind)
                    {
                        case FacadeKind.Arcade:
                            band.IsArcade = true;
                            break;
                        case FacadeKind.Residential:
                            for (var i = 0; i < n; i++)
                            {
                                var c = (i + 0.5f) * bay;
                                if (i == doorBay)
                                {
                                    var dw = rng.Range(1.0f, 1.2f);
                                    var dh = Mathf.Min(h - 0.45f, rng.Range(2.15f, 2.35f));
                                    band.Openings.Add(WallOpening.Make(c - dw * 0.5f, c + dw * 0.5f, band.V0, band.V0 + dh + (arched ? dw * 0.25f : 0f),
                                        arched ? OpeningShape.SegmentalArch : OpeningShape.Rect, depth, true, OpeningFill.Door, rng.Range(0, 9999), true));
                                }
                                else
                                {
                                    var top = Mathf.Min(band.V0 + h - 0.55f, band.V0 + 2.35f);
                                    band.Openings.Add(WallOpening.Make(c - winW * 0.5f, c + winW * 0.5f, band.V0 + 1.0f, top, OpeningShape.Rect, depth, true, OpeningFill.GrilledWindow, rng.Range(0, 9999)));
                                }
                            }
                            break;
                        case FacadeKind.Shops:
                            for (var i = 0; i < n; i++)
                            {
                                var c = (i + 0.5f) * bay;
                                var w = Mathf.Min(bay - 0.55f, rng.Range(2.2f, 2.8f));
                                var top = band.V0 + Mathf.Min(h - 0.75f, 2.75f);
                                var isDoor = i == doorBay && n > 2;
                                band.Openings.Add(isDoor
                                    ? WallOpening.Make(c - 0.55f, c + 0.55f, band.V0, band.V0 + 2.2f, OpeningShape.Rect, depth, true, OpeningFill.Door, rng.Range(0, 9999), true)
                                    : WallOpening.Make(c - w * 0.5f, c + w * 0.5f, band.V0, top, OpeningShape.Rect, depth, true, rng.Chance(0.8f) ? OpeningFill.RollerShutter : OpeningFill.Shopfront, rng.Range(0, 9999), true));
                            }
                            break;
                        case FacadeKind.Service:
                            {
                                var c = (doorBay + 0.5f) * bay;
                                band.Openings.Add(WallOpening.Make(c - 0.5f, c + 0.5f, band.V0, band.V0 + 2.1f, OpeningShape.Rect, depth, true, OpeningFill.Door, rng.Range(0, 9999), true));
                                for (var i = 0; i < n; i++)
                                {
                                    if (i == doorBay || !rng.Chance(0.6f)) continue;
                                    var cc = (i + 0.5f) * bay;
                                    band.Openings.Add(WallOpening.Make(cc - 0.35f, cc + 0.35f, band.V0 + 1.7f, band.V0 + Mathf.Min(h - 0.5f, 2.5f), OpeningShape.Rect, depth, true, OpeningFill.GrilledWindow, rng.Range(0, 9999)));
                                }
                            }
                            break;
                    }
                    continue;
                }
                // Upper storeys.
                for (var i = 0; i < n; i++)
                {
                    var c = (i + 0.5f) * bay;
                    if (kind == FacadeKind.Service)
                    {
                        if (!rng.Chance(0.55f)) continue;
                        band.Openings.Add(WallOpening.Make(c - 0.4f, c + 0.4f, band.V0 + 1.15f, band.V0 + Mathf.Min(h - 0.45f, 2.15f), OpeningShape.Rect, depth, true, OpeningFill.ShutteredWindow, rng.Range(0, 9999)));
                        continue;
                    }
                    var segmental = arched && rng.Chance(0.5f);
                    if (s == balconyStorey && balconyBays[i])
                    {
                        var bw = Mathf.Min(bay - 0.7f, rng.Range(1.0f, 1.2f));
                        var top = band.V0 + Mathf.Min(h - 0.45f, 2.35f);
                        band.Openings.Add(WallOpening.Make(c - bw * 0.5f, c + bw * 0.5f, band.V0 + 0.02f, top, segmental ? OpeningShape.SegmentalArch : OpeningShape.Rect,
                            depth, true, OpeningFill.BalconyDoor, rng.Range(0, 9999), true));
                        band.Balconies.Add(new Vector2(i * bay + 0.25f, (i + 1) * bay - 0.25f));
                        continue;
                    }
                    var wh = Mathf.Min(h - 1.35f, rng.Range(1.3f, 1.6f));
                    var sill = band.V0 + 0.9f;
                    band.Openings.Add(WallOpening.Make(c - winW * 0.5f, c + winW * 0.5f, sill, sill + wh + (segmental ? winW * 0.25f : 0f),
                        segmental ? OpeningShape.SegmentalArch : OpeningShape.Rect, depth, true, OpeningFill.ShutteredWindow, rng.Range(0, 9999)));
                }
                // Merge adjacent balcony bays into one balcony.
                MergeBalconies(band.Balconies);
            }

            // Explicit features replace composed openings they overlap.
            foreach (var feat in spec.Features)
            {
                if (feat.Side != side) continue;
                foreach (var band in bands)
                {
                    if (feat.V1 <= band.V0 + 0.01f || feat.V0 >= band.V1 - 0.01f) continue;
                    band.Openings.RemoveAll(o => o.U1 > feat.U0 - 0.25f && o.U0 < feat.U1 + 0.25f);
                    band.Balconies.RemoveAll(b => b.y > feat.U0 && b.x < feat.U1);
                    if (feat.V0 >= band.V0 - 0.01f && feat.V0 < band.V1)
                    {
                        band.Openings.Add(WallOpening.Make(feat.U0, feat.U1, feat.V0, Mathf.Min(feat.V1, band.V1), feat.Arched ? OpeningShape.RoundArch : OpeningShape.Rect,
                            depth, !feat.Through, (OpeningFill)feat.Fill, feat.Tag != null ? feat.Tag.GetHashCode() : 0, true));
                    }
                }
            }
            return bands;
        }

        private static void MergeBalconies(List<Vector2> spans)
        {
            spans.Sort((a, b) => a.x.CompareTo(b.x));
            for (var i = spans.Count - 1; i > 0; i--)
            {
                if (spans[i].x - spans[i - 1].y < 0.6f)
                {
                    spans[i - 1] = new Vector2(spans[i - 1].x, spans[i].y);
                    spans.RemoveAt(i);
                }
            }
        }
    }
}
