import { initSite } from './site';
import './components/usage-widget';
import { initCommunity } from './community/controller';
import { initMotion } from './features/motion';
import { initCreator } from './community/creation/controller';

initSite();
initCommunity();
initCreator();
initMotion();
