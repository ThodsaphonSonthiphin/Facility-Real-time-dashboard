/** A reading next to the seed's AR01 signs (a made-up place), and one about 80 m away with a poor fix. */
export const nearSign = { latitude: 13.7563, longitude: 100.5018, accuracy: 12 };
export const farFromSign = { latitude: 13.757, longitude: 100.5018, accuracy: 40 };

export type FakeGpsAnswer = typeof nearSign | 'denied' | 'unavailable';

/**
 * Gives the page a navigator.geolocation (jsdom has none). Each getCurrentPosition takes the next answer; the last
 * one repeats. Like fetch, it is a browser API, not our code (regression-net #32). test/setup.ts removes it after
 * each test. `calls` counts the readings asked for.
 */
export function fakeGps(...answers: FakeGpsAnswer[]) {
  const state = { calls: 0 };
  const geolocation = {
    getCurrentPosition(onFix: PositionCallback, onError?: PositionErrorCallback | null) {
      const answer = answers[Math.min(state.calls, answers.length - 1)];
      state.calls += 1;
      setTimeout(() => {
        if (answer === 'denied' || answer === 'unavailable') {
          const code = answer === 'denied' ? 1 : 2;
          onError?.({ code, message: answer, PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 } as unknown as GeolocationPositionError);
        } else {
          const coords = { ...answer, altitude: null, altitudeAccuracy: null, heading: null, speed: null };
          onFix({ coords, timestamp: Date.now() } as unknown as GeolocationPosition);
        }
      }, 0);
    },
  };
  Object.defineProperty(navigator, 'geolocation', { value: geolocation, configurable: true });
  return state;
}
