import type { AvailableRoom } from '../api/availabilityApi';

const standardRoomImages = [
  'https://images.pexels.com/photos/271624/pexels-photo-271624.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/164595/pexels-photo-164595.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/2029722/pexels-photo-2029722.jpeg?auto=compress&cs=tinysrgb&w=1200',
];

const deluxeRoomImages = [
  'https://images.pexels.com/photos/1571460/pexels-photo-1571460.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/262048/pexels-photo-262048.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/237371/pexels-photo-237371.jpeg?auto=compress&cs=tinysrgb&w=1200',
];

const suiteRoomImages = [
  'https://images.pexels.com/photos/1457842/pexels-photo-1457842.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/2034335/pexels-photo-2034335.jpeg?auto=compress&cs=tinysrgb&w=1200',
  'https://images.pexels.com/photos/261102/pexels-photo-261102.jpeg?auto=compress&cs=tinysrgb&w=1200',
];

const fallbackRoomImages = [
  ...standardRoomImages,
  ...deluxeRoomImages,
  ...suiteRoomImages,
];

export function getRoomImage(room: AvailableRoom): string {
  const roomType = room.roomTypeName.trim().toLowerCase();
  const images = getImagesByRoomType(roomType);
  const uniqueNumber = Math.abs(room.roomId) + safeNumberFromRoomLabel(room.roomNumber);
  const index = uniqueNumber % images.length;

  return images[index];
}

function getImagesByRoomType(roomType: string): string[] {
  if (roomType.includes('standard')) {
    return standardRoomImages;
  }

  if (roomType.includes('deluxe')) {
    return deluxeRoomImages;
  }

  if (roomType.includes('suite')) {
    return suiteRoomImages;
  }

  return fallbackRoomImages;
}

function safeNumberFromRoomLabel(roomNumber: string): number {
  const numericValue = Number.parseInt(roomNumber.replace(/\D/g, ''), 10);
  return Number.isNaN(numericValue) ? 0 : numericValue;
}
