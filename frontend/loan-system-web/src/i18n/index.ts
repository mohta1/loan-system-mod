import i18n from 'i18next'; import { initReactI18next } from 'react-i18next';
export const resources={en:{translation:{title:'Loan System',subtitle:'Runtime status',checking:'Checking system status…',operational:'Operational',unavailable:'API unavailable',language:'العربية',version:'Version'}},ar:{translation:{title:'نظام القروض',subtitle:'حالة النظام',checking:'جارٍ التحقق من حالة النظام…',operational:'يعمل',unavailable:'واجهة البرمجة غير متاحة',language:'English',version:'الإصدار'}}};
void i18n.use(initReactI18next).init({resources,lng:'en',fallbackLng:'en',interpolation:{escapeValue:false}});
export function applyLanguage(language:'en'|'ar'){void i18n.changeLanguage(language); document.documentElement.lang=language; document.documentElement.dir=language==='ar'?'rtl':'ltr';}
export default i18n;
