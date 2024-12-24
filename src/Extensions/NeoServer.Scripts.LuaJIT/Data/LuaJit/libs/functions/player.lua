﻿function Player.sendCancelMessage(self, message)
	print('type(message): ' .. type(message))
	if type(message) == "number" then
		message = Game.getReturnMessage(message)
	end
	return self:sendTextMessage(MESSAGE_STATUS_SMALL, message)
end