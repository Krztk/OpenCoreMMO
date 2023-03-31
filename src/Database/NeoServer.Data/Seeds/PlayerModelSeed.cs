﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoServer.Data.Model;
using NeoServer.Game.Common.Creatures.Players;

namespace NeoServer.Data.Seeds;

public class PlayerModelSeed
{
    public static void Seed(EntityTypeBuilder<PlayerModel> builder)
    {
        builder.HasData
        (
            new PlayerModel
            {
                PlayerId = 1,
                PlayerType = 3,
                AccountId = 1,
                TownId = 1,
                Name = "GOD",
                ChaseMode = ChaseMode.Follow,
                Capacity = 90000,
                Level = 1000,
                Health = 4440,
                MaxHealth = 4440,
                Vocation = 11,
                Gender = Gender.Male,
                Speed = 800,
                Online = false,
                Mana = 1750,
                MaxMana = 1750,
                Soul = 100,
                MaxSoul = 100,
                PosX = 1020,
                PosY = 1022,
                PosZ = 7,
                StaminaMinutes = 2520,
                LookType = 75,
                SkillAxe = byte.MaxValue,
                SkillSword = byte.MaxValue,
                SkillClub = byte.MaxValue,
                SkillDist = byte.MaxValue,
                SkillFishing = byte.MaxValue,
                SkillFist = byte.MaxValue,
                MagicLevel = byte.MaxValue,
                SkillShielding = byte.MaxValue,
                Experience = 0,
                FightMode = FightMode.Attack,
                WorldId = 1
            },
            new PlayerModel
            {
                PlayerId = 2,
                PlayerType = 1,
                AccountId = 1,
                TownId = 1,
                Name = "Sorcerer Sample",
                ChaseMode = ChaseMode.Follow,
                Capacity = 5390,
                Level = 500,
                Health = 2645,
                MaxHealth = 2645,
                Vocation = 1,
                Gender = Gender.Male,
                Speed = 800,
                Online = false,
                Mana = 14850,
                MaxMana = 14850,
                Soul = 100,
                MaxSoul = 100,
                PosX = 1020,
                PosY = 1022,
                PosZ = 7,
                StaminaMinutes = 2520,
                LookType = 130,
                LookBody = 69,
                LookFeet = 95,
                LookHead = 78,
                LookLegs = 58,
                LookAddons = 0,
                SkillAxe = 60,
                SkillSword = 60,
                SkillClub = 60,
                SkillDist = 60,
                SkillFishing = 60,
                SkillFist = 60,
                MagicLevel = 60,
                SkillShielding = 60,
                Experience = 2058474800,
                FightMode = FightMode.Attack,
                WorldId = 1
            },
            new PlayerModel
            {
                PlayerId = 3,
                PlayerType = 1,
                AccountId = 1,
                TownId = 1,
                Name = "Knight Sample",
                ChaseMode = ChaseMode.Follow,
                Capacity = 12770,
                Level = 500,
                Health = 4440,
                MaxHealth = 4440,
                Vocation = 4,
                Gender = Gender.Male,
                Speed = 800,
                Online = false,
                Mana = 1750,
                MaxMana = 1750,
                Soul = 100,
                MaxSoul = 100,
                PosX = 1020,
                PosY = 1022,
                PosZ = 7,
                StaminaMinutes = 2520,
                LookType = 131,
                LookBody = 69,
                LookFeet = 95,
                LookHead = 78,
                LookLegs = 58,
                LookAddons = 0,
                SkillAxe = 60,
                SkillSword = 60,
                SkillClub = 60,
                SkillDist = 60,
                SkillFishing = 60,
                SkillFist = 60,
                MagicLevel = 60,
                SkillShielding = 60,
                Experience = 2058474800,
                FightMode = FightMode.Attack,
                WorldId = 1
            },
            new PlayerModel
            {
                PlayerId = 4,
                PlayerType = 1,
                AccountId = 1,
                TownId = 1,
                Name = "Druid Sample",
                ChaseMode = ChaseMode.Follow,
                Capacity = 5390,
                Level = 500,
                Health = 4440,
                MaxHealth = 4440,
                Vocation = 2,
                Gender = Gender.Male,
                Speed = 800,
                Online = false,
                Mana = 1750,
                MaxMana = 1750,
                Soul = 100,
                MaxSoul = 100,
                PosX = 1020,
                PosY = 1022,
                PosZ = 7,
                StaminaMinutes = 2520,
                LookType = 130,
                LookBody = 69,
                LookFeet = 95,
                LookHead = 78,
                LookLegs = 58,
                LookAddons = 0,
                SkillAxe = 60,
                SkillSword = 60,
                SkillClub = 60,
                SkillDist = 60,
                SkillFishing = 60,
                SkillFist = 60,
                MagicLevel = 60,
                SkillShielding = 60,
                Experience = 2058474800,
                FightMode = FightMode.Attack,
                WorldId = 1
            },
            new PlayerModel
            {
                PlayerId = 5,
                PlayerType = 1,
                AccountId = 2,
                TownId = 1,
                Name = "Paladin",
                ChaseMode = ChaseMode.Follow,
                Capacity = 10310,
                Level = 500,
                Health = 4440,
                MaxHealth = 4440,
                Vocation = 3,
                Gender = Gender.Female,
                Speed = 800,
                Online = false,
                Mana = 1750,
                MaxMana = 1750,
                Soul = 100,
                MaxSoul = 100,
                PosX = 1020,
                PosY = 1022,
                PosZ = 7,
                StaminaMinutes = 2520,
                LookType = 137,
                LookBody = 69,
                LookFeet = 95,
                LookHead = 78,
                LookLegs = 58,
                LookAddons = 0,
                SkillAxe = 60,
                SkillSword = 60,
                SkillClub = 60,
                SkillDist = 60,
                SkillFishing = 60,
                SkillFist = 60,
                MagicLevel = 60,
                SkillShielding = 60,
                Experience = 2058474800,
                FightMode = FightMode.Attack,
                WorldId = 1
            }
        );
    }
}
