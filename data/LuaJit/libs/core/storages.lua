--[[
	It is possible to place the storage in a quest door, so the player who has that storage will go through the door

	Reserved player action storage key ranges (const.hpp at the source)
	[10000000 - 20000000]
	[1000 - 1500]
	[2001 - 2011]

	Others reserved player action/storages
	[100] = unmovable/untrade/unusable items
	[101] = use pick floor
	[102] = well down action
	[103-120] = others keys action
	[103] = key 0010
	[303] = key 0303
	[1000] = level door. Here 1 must be used followed by the level.
	Example: 1010 = level 10,
	1100 = level 100]

	Questline = Storage through the Quest
]]

Storage = {
	Quest = {
		ExampleQuest = {
			Example = 9000,
		},
		SpikeSwordQuest = {
			Key = 9001
		},
		DwarvenShieldQuest = {
			Key = 9002
		},
		DarkHelmetQuest = {
			Key = 9003
		},
		CombatKnifeQuest = {
			Key = 9003
		},
	},

	Example = 30002,
}

GlobalStorage = {
	ExampleQuest = {
		Example = 60000,
	},

	Example = 60001,
}
