"""A paper model of the tower's difficulty curve, from the formulas in the code.
  python Tools/balance_tower.py            prints swings-to-kill per floor, before and after
  python Tools/balance_tower.py <out.csv>  also writes the table

It follows a hero who clears every floor once, puts half of every level's stat points into
Strength, and takes the damage boon from the first Guardian. It is a model, not a play-test:
it exists to check that a change to Enemy.cs moves the curve the way the testers asked."""
import sys

BASE_DAMAGE = 10.0
IMP_HP, BRUTE_HP = 26.0, 220.0


def xp_needed(level):                      # GameTower.XpNeeded
    return 10 + level * 12 + level * level * 3


def floor_wave(floor):                     # GameTower.FloorWave, averaged
    budget = 3.5 + floor * 1.2
    brutes = (1 + floor // 15) if floor % 5 == 0 else 0
    if brutes:
        budget -= 4
    cost = 1.0 if floor < 2 else 1.3 if floor < 5 else 1.6 if floor < 12 else 1.9
    return min(16, max(1, round(budget / cost))), brutes


def health_scale_old(floor):               # Enemy.Init before the change
    chapter = (floor - 1) // 10
    return (1 + 0.35 * chapter) * (1 + 0.03 * (floor - 1))


def health_scale_new(floor):               # and after: the same to floor 10, steeper above it
    chapter = (floor - 1) // 10
    return (1 + 0.35 * chapter) * (1 + 0.03 * (floor - 1) + 0.07 * max(0, floor - 10))


def climb(scale):
    level, xp, points, rows = 1, 0.0, 0, []
    for floor in range(1, 31):
        strength = points // 2
        damage = BASE_DAMAGE * (1 + 0.02 * (level - 1)) * (1 + 0.04 * strength)
        if floor > 5:
            damage *= 1.18                 # "Sharper Bristles", a common first boon
        rows.append((floor, level, strength, damage, IMP_HP * scale(floor) / damage, BRUTE_HP * scale(floor) / damage))
        count, brutes = floor_wave(floor)
        worth = (count * 7.5 + brutes * 50) * (1 + 0.1 * (floor - 1)) * 1.2     # KillXp at a B style rank
        xp += worth
        while xp >= xp_needed(level):
            xp -= xp_needed(level)
            level += 1
            points += 3
        if floor in (4, 9, 14, 19, 24):
            points += 2                    # a rescued Painter
    return rows


if __name__ == "__main__":
    old, new = climb(health_scale_old), climb(health_scale_new)
    lines = ["floor,hero_level,strength,hero_damage,imp_swings_before,imp_swings_after,brute_swings_before,brute_swings_after"]
    for a, b in zip(old, new):
        lines.append("%d,%d,%d,%.1f,%.2f,%.2f,%.1f,%.1f" % (a[0], a[1], a[2], a[3], a[4], b[4], a[5], b[5]))
    print("\n".join(lines))
    if len(sys.argv) > 1:
        open(sys.argv[1], "w").write("\n".join(lines) + "\n")
