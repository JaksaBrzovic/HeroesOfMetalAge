using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge
{
    [Serializable] public class Troop
    {
        public int kind, count;
        public Troop(int kind, int count) { this.kind = kind; this.count = count; }
        public Troop Copy() => new Troop(kind, count);
    }
    [Serializable] public class Campaign
    {
        public int version = 1, day = 1, gold = 1600, xp, x = 2, y = 5, movement = 16;
        public bool joined, sentinelDead, bossDead, townOwned, heroHome, recruited;
        public List<Troop> army = new List<Troop>();
        public List<Troop> garrison = new List<Troop>();
        public int[] stock = { 14, 9, 4 };
        public List<int> bag = new List<int>();
        public int weapon = -1, charm = -1;
        public static Campaign New()
        {
            var c = new Campaign(); c.army.Add(new Troop(0, 12)); return c;
        }
    }
    public class Creature
    {
        public string name, subtitle, voice;
        public int hp, min, max, attack, defense, speed, cost, growth;
        public bool ranged;
        public Creature(string n, string sub, int h, int lo, int hi, int a, int d, int s, int c, int g, bool r, string v)
        { name=n; subtitle=sub; hp=h; min=lo; max=hi; attack=a; defense=d; speed=s; cost=c; growth=g; ranged=r; voice=v; }
    }
    public static class Rules
    {
        public const int Slots = 7, DailyMovement = 16;
        public static readonly Creature[] Units = {
            new Creature("IRONBOUND", "Axe infantry", 18, 3, 5, 5, 4, 4, 70, 14, false, "ironbound"),
            new Creature("ASH ARCHER", "Ranged / half damage in melee", 13, 3, 6, 6, 3, 5, 110, 9, true, "archer"),
            new Creature("DOOM RIDER", "Heavy cavalry", 40, 7, 11, 9, 8, 6, 260, 4, false, "rider")
        };
        public static readonly string[] ArtifactNames = { "Fang of Thunder", "Ashen Ward", "Warborn Sigil" };
        public static readonly string[] ArtifactDescriptions = { "Weapon / +2 hero attack", "Charm / +2 hero defense", "Charm / +1 creature speed" };
        public static int Level(Campaign c) => 1 + c.xp / 300;
        public static int Attack(Campaign c) => 2 + Level(c) - 1 + (c.weapon == 0 ? 2 : 0);
        public static int Defense(Campaign c) => 2 + (Level(c) - 1) / 2 + (c.charm == 1 ? 2 : 0);
        public static int SpeedBonus(Campaign c) => c.charm == 2 ? 1 : 0;
        public static bool CanAdd(List<Troop> army, int kind) => army.Count < Slots || army.Exists(t => t.kind == kind);
        public static bool Add(List<Troop> army, int kind, int count)
        {
            if (count <= 0 || !CanAdd(army, kind)) return false;
            var same = army.Find(t => t.kind == kind);
            if (same != null) same.count += count; else army.Add(new Troop(kind, count)); return true;
        }
        public static int Price(Campaign c, int kind) => c.townOwned ? Mathf.CeilToInt(Units[kind].cost * .55f) : Units[kind].cost;
        public static bool Buy(Campaign c, int kind, int count)
        {
            if (kind < 0 || kind >= Units.Length || count <= 0 || count > c.stock[kind] || !CanAdd(c.army, kind)) return false;
            long total = (long)Price(c, kind) * count;
            if (total > c.gold) return false;
            Add(c.army, kind, count); c.stock[kind] -= count; c.gold -= (int)total; c.recruited=true; return true;
        }
        public static bool EndDay(Campaign c)
        {
            c.day++; c.movement = DailyMovement;
            if ((c.day-1) % 7 != 0) return false;
            for(int i=0;i<Units.Length;i++) c.stock[i] += Units[i].growth;
            return true;
        }
        public static bool Transfer(Campaign c, bool fromGarrison, int slot)
        {
            if (!c.townOwned || c.heroHome) return false;
            var source = fromGarrison ? c.garrison : c.army; var target = fromGarrison ? c.army : c.garrison;
            if (slot < 0 || slot >= source.Count || !CanAdd(target, source[slot].kind)) return false;
            var t=source[slot]; Add(target,t.kind,t.count); source.RemoveAt(slot); return true;
        }
        public static bool StationHero(Campaign c)
        {
            if(!c.townOwned || c.x!=11 || c.y!=5) return false;
            if(c.heroHome) { foreach(var t in c.garrison) if(!CanAdd(c.army,t.kind)) return false; foreach(var t in c.garrison) Add(c.army,t.kind,t.count); c.garrison.Clear(); c.heroHome=false; }
            else { foreach(var t in c.army) if(!CanAdd(c.garrison,t.kind)) return false; foreach(var t in c.army) Add(c.garrison,t.kind,t.count); c.army.Clear(); c.heroHome=true; }
            return true;
        }
        public static int Damage(int kind, int number, int heroAttack, int defenderKind, int heroDefense, bool defend, bool melee, float roll)
        {
            var a=Units[kind]; var d=Units[defenderKind]; int delta=a.attack+heroAttack-d.defense-heroDefense;
            float multiplier=delta>=0 ? 1f+Mathf.Min(delta,30)*.05f : 1f-Mathf.Min(-delta,28)*.025f;
            float damage=number*Mathf.Lerp(a.min,a.max,Mathf.Clamp01(roll))*multiplier;
            if(defend) damage *= .7f; if(a.ranged && melee) damage *= .5f;
            return Mathf.Max(1,Mathf.RoundToInt(damage));
        }
        public static bool Valid(Campaign c)
        {
            if(c==null || c.version!=1 || c.day<1 || c.gold<0 || c.xp<0 || c.x<0 || c.x>=18 || c.y<0 || c.y>=11 || c.movement<0 || c.movement>DailyMovement) return false;
            if(c.army==null || c.garrison==null || c.stock==null || c.stock.Length!=3 || c.bag==null || c.weapon < -1 || c.weapon>0 || c.charm < -1 || c.charm>2 || c.charm==0) return false;
            foreach(var list in new[]{c.army,c.garrison}) {
                if(list.Count>Slots) return false;
                foreach(var t in list) if(t==null || t.kind<0 || t.kind>=3 || t.count<=0 || t.count>100000) return false;
            }
            foreach(int s in c.stock) if(s<0 || s>100000) return false;
            foreach(int a in c.bag) if(a<0 || a>=3) return false;
            return (c.army.Count>0 || (c.townOwned&&c.garrison.Count>0)) && (!c.heroHome || (c.townOwned&&c.army.Count==0&&c.x==11&&c.y==5));
        }
    }
}
