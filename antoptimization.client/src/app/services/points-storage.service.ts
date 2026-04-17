import { Injectable } from '@angular/core';
import { LocationDto } from '../models/route.models';

export interface SavedPoints {
  locations: LocationDto[];
  startIndex: number | null;
}

@Injectable({ providedIn: 'root' })
export class PointsStorageService {
  private readonly STORAGE_KEY = 'antopt_saved_points';

  saveToLocalStorage(locations: LocationDto[], startIndex: number | null): void {
    const data: SavedPoints = { locations: [...locations], startIndex };
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
  }

  loadFromLocalStorage(): SavedPoints | null {
    const raw = localStorage.getItem(this.STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as SavedPoints;
    } catch {
      return null;
    }
  }

  hasSavedPoints(): boolean {
    return localStorage.getItem(this.STORAGE_KEY) !== null;
  }

  clearSavedPoints(): void {
    localStorage.removeItem(this.STORAGE_KEY);
  }

  exportToCsv(locations: LocationDto[], startIndex: number | null): void {
    const lines = ['lat,lng,is_start'];
    locations.forEach((loc, i) => {
      lines.push(`${loc.lat},${loc.lng},${i === startIndex}`);
    });
    const csv = lines.join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'pontos_rota.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }

  parseCsv(content: string): SavedPoints {
    const lines = content.split('\n').map(l => l.trim()).filter(l => l.length > 0);

    if (lines.length === 0 || lines[0].toLowerCase() !== 'lat,lng,is_start') {
      throw new Error('CSV inválido: o cabeçalho deve ser "lat,lng,is_start".');
    }

    const locations: LocationDto[] = [];
    let startIndex: number | null = null;

    for (let i = 1; i < lines.length; i++) {
      const parts = lines[i].split(',');
      if (parts.length < 3) {
        throw new Error(`CSV inválido: linha ${i + 1} mal formatada.`);
      }
      const lat = parseFloat(parts[0]);
      const lng = parseFloat(parts[1]);
      if (isNaN(lat) || isNaN(lng)) {
        throw new Error(`CSV inválido: linha ${i + 1} contém coordenadas inválidas.`);
      }
      const isStart = parts[2].trim().toLowerCase() === 'true';
      if (isStart) {
        startIndex = locations.length;
      }
      locations.push({ lat, lng });
    }

    return { locations, startIndex };
  }
}
