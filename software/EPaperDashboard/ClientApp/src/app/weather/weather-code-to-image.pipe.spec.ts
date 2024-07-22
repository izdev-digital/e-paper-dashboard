import { WeatherCodeToImagePipe } from './weather-code-to-image.pipe';

describe('WeatherCodeToImagePipe', () => {
  it('create an instance', () => {
    const pipe = new WeatherCodeToImagePipe();
    expect(pipe).toBeTruthy();
  });
});
